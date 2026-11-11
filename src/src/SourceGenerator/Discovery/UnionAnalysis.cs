using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Purview.Unions.SourceGenerator.Common;
using Purview.Unions.SourceGenerator.Diagnostics;
using Purview.Unions.SourceGenerator.Models;

namespace Purview.Unions.SourceGenerator.Discovery;

/// <summary>
/// Resolves one <c>[GenerateUnion]</c> target into the immutable model the pipeline caches, and produces
/// every finding that decides whether it can be generated.
/// </summary>
/// <remarks>
/// <para>
/// This is the single implementation of the per-target rules, used by both the analyzer and the source
/// generator so the two hosts cannot drift apart. Roslyn symbols are used only while analysing; the model
/// holds value-equatable data (<c>TypeReference</c>, <c>EquatableArray&lt;T&gt;</c> and source spans) and
/// never a syntax node, symbol, semantic model or compilation.
/// </para>
/// <para>
/// Case lists are resolved in the order the declaration surfaces are documented in: the
/// <c>[GenerateUnion]</c> <c>typeof</c> list, the union's own partial static factory signatures, a nested
/// <c>IUnionMembers</c> provider, then existing public single-parameter constructors. More than one
/// differing list is <c>UNI0014</c>.
/// </para>
/// </remarks>
static class UnionAnalysis
{
	/// <summary>
	/// The display format used for diagnostics, keeping generic arguments, language keywords and escaped
	/// identifiers.
	/// </summary>
	public static readonly SymbolDisplayFormat DisplayFormat = new(
		typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
			| SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
	);

	/// <summary>
	/// The display format used for emitted type names: fully qualified, with C# keywords and nullable
	/// annotations. A type parameter renders as its bare name.
	/// </summary>
	static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
		SymbolDisplayMiscellaneousOptions.UseSpecialTypes
			| SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
			| SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
	);

	/// <summary>
	/// Renders a type as a literal, render-only reference.
	/// </summary>
	/// <remarks>
	/// A <c>TypeReference</c> built from a symbol carries a <c>TypeIdentity</c> whose name is empty for a
	/// type parameter, which <c>CodeWriter</c> rejects in a declaration. The generator emits fully
	/// qualified names, so a literal reference over the rendered name is both exact and safe for arrays,
	/// nullable types, constructed generics and type parameters.
	/// </remarks>
	static TypeReference ToLiteral(ITypeSymbol type) => new(new TypeIdentity(type.ToDisplayString(TypeFormat), null));

	/// <summary>
	/// Determines whether the target opted in with <c>[GenerateUnion]</c>.
	/// </summary>
	/// <param name="symbol">The target symbol.</param>
	/// <returns><see langword="true"/> when the attribute is applied.</returns>
	public static bool HasGenerateUnionAttribute(INamedTypeSymbol symbol)
	{
		foreach (var attribute in symbol.GetAttributes())
		{
			if (UnionsTypeLibrary.GenerateUnionAttribute.Matches(attribute.AttributeClass))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Analyzes one opted-in target.
	/// </summary>
	/// <param name="union">The attributed type.</param>
	/// <param name="compilation">The compilation the symbol belongs to.</param>
	/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
	/// <returns>The model, when generation can continue, and every finding.</returns>
	public static UnionAnalysisResult Analyze(
		INamedTypeSymbol union,
		Compilation compilation,
		CancellationToken cancellationToken
	)
	{
		cancellationToken.ThrowIfCancellationRequested();

		var findings = ImmutableArray.CreateBuilder<ReportableDiagnostic>();
		var displayName = union.ToDisplayString(DisplayFormat);
		var declarationLocation = union.Locations.IsDefaultOrEmpty ? Location.None : union.Locations[0];
		var attribute = FindGenerateUnionAttribute(union);
		var attributeLocation = GetLocation(attribute) ?? declarationLocation;

		if (
			TryValidateDeclaration(
				union,
				displayName,
				declarationLocation,
				attributeLocation,
				findings,
				cancellationToken
			)
			is not { } declaration
		)
			return Blocked(findings);

		if (
			TryResolveCases(
				union,
				attribute,
				displayName,
				declarationLocation,
				attributeLocation,
				findings,
				cancellationToken
			)
			is not { } resolution
		)
			return Blocked(findings);

		if (
			TryCreateCases(
				union,
				resolution,
				compilation,
				displayName,
				declarationLocation,
				attributeLocation,
				findings,
				cancellationToken
			)
			is not { } cases
		)
			return Blocked(findings);

		if (
			TryFinalise(
				union,
				resolution.Source,
				resolution.Cases,
				resolution.ConstructorCases,
				displayName,
				declarationLocation,
				findings
			)
			is not { } emitMembers
		)
			return Blocked(findings);

		var namespaceName = union.ContainingNamespace.IsGlobalNamespace
			? string.Empty
			: union.ContainingNamespace.ToDisplayString();

		UnionModel model = new(
			Namespace: namespaceName,
			IsGlobalNamespace: namespaceName.Length == 0,
			UnionType: new TypeIdentity(union),
			Name: union.Name,
			FullyQualifiedName: displayName,
			Kind: declaration.Kind,
			Accessibility: ToAccessibility(union.DeclaredAccessibility),
			IsReadOnly: union.IsReadOnly,
			TypeParameters: CreateTypeParameters(union),
			ContainingTypes: declaration.ContainingTypes,
			Cases: cases,
			Factories: resolution.Factories,
			CaseSource: resolution.Source,
			EmitMembers: emitMembers,
			EmitUnionAttribute: !HasUnionAttribute(union),
			TagTypeName: PropertyLibrary.GetTagTypeName(cases.Length),
			HintName: CreateHintName(union, namespaceName, declaration.ContainingTypes),
			Location: ToSourceLocation(declarationLocation)
		);

		return new UnionAnalysisResult(model, findings.ToImmutable());
	}

	static DeclarationValidation? TryValidateDeclaration(
		INamedTypeSymbol union,
		string displayName,
		Location declarationLocation,
		Location attributeLocation,
		ImmutableArray<ReportableDiagnostic>.Builder findings,
		CancellationToken cancellationToken
	)
	{
		if (GetDeclarationKind(union) is not { } declarationKind)
		{
			Add(findings, DiagnosticLibrary.UnsupportedDeclarationKind, attributeLocation, displayName);

			return null;
		}

		if (union.IsStatic || (union.TypeKind == TypeKind.Class && union.IsAbstract) || union.IsRefLikeType)
		{
			Add(findings, DiagnosticLibrary.UnsupportedDeclarationKind, attributeLocation, displayName);

			return null;
		}

		if (!IsPartial(union, cancellationToken))
		{
			Add(findings, DiagnosticLibrary.UnionMustBePartial, attributeLocation, displayName);

			return null;
		}

		var builder = ImmutableArray.CreateBuilder<ContainingTypeModel>();
		for (var current = union.ContainingType; current is not null; current = current.ContainingType)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (
				current.Arity > 0
				|| !IsPartial(current, cancellationToken)
				|| GetDeclarationKind(current) is not { } containingKind
			)
			{
				Add(findings, DiagnosticLibrary.ContainingTypeCannotBeNamed, declarationLocation, displayName);

				return null;
			}

			builder.Insert(
				0,
				new ContainingTypeModel(
					current.Name,
					containingKind,
					ToAccessibility(current.DeclaredAccessibility),
					current.IsReadOnly,
					current.IsStatic
				)
			);
		}

		return new DeclarationValidation(declarationKind, builder.ToImmutable());
	}

	static CaseResolution? TryResolveCases(
		INamedTypeSymbol union,
		AttributeData? attribute,
		string displayName,
		Location declarationLocation,
		Location attributeLocation,
		ImmutableArray<ReportableDiagnostic>.Builder findings,
		CancellationToken cancellationToken
	)
	{
		var constructorCases = GetConstructorCases(union, cancellationToken);

		var sources = new[]
		{
			(Source: UnionCaseSource.Attribute, Cases: GetAttributeCases(attribute)),
			(Source: UnionCaseSource.Factories, Cases: GetFactoryCases(union, cancellationToken)),
			(Source: UnionCaseSource.Provider, Cases: GetProviderCases(union, cancellationToken)),
			(Source: UnionCaseSource.Constructors, Cases: constructorCases),
		};

		var present = sources.Where(static source => !source.Cases.IsDefaultOrEmpty).ToArray();
		if (present.Length == 0)
		{
			Add(findings, DiagnosticLibrary.UnionHasNoCases, declarationLocation, displayName);

			return null;
		}

		var source = present[0].Source;
		var declaredCases = present[0].Cases;

		if (
			present.Length > 1
			&& present
				.Skip(1)
				.Any(source =>
					source.Source != UnionCaseSource.Constructors && !CaseSetsEqual(declaredCases, source.Cases)
				)
		)
		{
			Add(findings, DiagnosticLibrary.ConflictingCaseSources, attributeLocation, displayName);

			return null;
		}

		// A typeof list cannot name a type parameter, so a generic union must use another surface.
		if (source == UnionCaseSource.Attribute && union.Arity > 0)
		{
			Add(findings, DiagnosticLibrary.GenericUnionNeedsSourceCaseList, attributeLocation, displayName);

			return null;
		}

		var factories =
			source == UnionCaseSource.Factories
				? GetFactories(union, cancellationToken)
				: EquatableArray<UnionFactoryModel>.Empty;

		return new CaseResolution(source, declaredCases, factories, constructorCases);
	}

	static ImmutableArray<UnionCaseModel>? TryCreateCases(
		INamedTypeSymbol union,
		CaseResolution resolution,
		Compilation compilation,
		string displayName,
		Location declarationLocation,
		Location attributeLocation,
		ImmutableArray<ReportableDiagnostic>.Builder findings,
		CancellationToken cancellationToken
	)
	{
		var builder = ImmutableArray.CreateBuilder<UnionCaseModel>(resolution.Cases.Length);
		List<ITypeSymbol> exposedTypes = [with(resolution.Cases.Length)];
		HashSet<string> seen = [with(StringComparer.Ordinal)];
		var blocked = false;

		foreach (var caseSymbol in resolution.Cases)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var caseLocation = GetSourceLocation(caseSymbol, attributeLocation);
			var caseName = caseSymbol.ToDisplayString(DisplayFormat);

			if (GetCaseTypeProblem(caseSymbol) is { } problem)
			{
				Add(findings, DiagnosticLibrary.UnsupportedCaseType, caseLocation, caseName, problem);
				blocked = true;
				continue;
			}

			if (
				!TryNormalizeCase(
					caseSymbol,
					out var exposed,
					out var caseType,
					out var parameterType,
					out var storageType
				)
			)
			{
				Add(findings, DiagnosticLibrary.UnusableNullableCase, caseLocation, caseName, displayName);
				blocked = true;
				continue;
			}

			if (!IsCaseAccessible(exposed, union, compilation))
			{
				Add(findings, DiagnosticLibrary.InaccessibleCaseType, caseLocation, caseName, displayName);
				blocked = true;
				continue;
			}

			var identity = exposed.ToDisplayString(DisplayFormat);
			if (!seen.Add(identity))
			{
				Add(findings, DiagnosticLibrary.DuplicateCaseType, caseLocation, caseName, displayName);
				blocked = true;
				continue;
			}

			exposedTypes.Add(exposed);
			builder.Add(
				new UnionCaseModel(
					CaseType: caseType,
					ParameterType: parameterType,
					StorageType: storageType,
					FullyQualifiedName: identity,
					IsReferenceType: exposed.IsReferenceType,
					IsNullableValueType: !SymbolEqualityComparer.Default.Equals(exposed, caseSymbol),
					IsTypeParameter: exposed is ITypeParameterSymbol,
					Tag: builder.Count + 1
				)
			);
		}

		if (blocked)
			return null;

		if (builder.Count > PropertyLibrary.MaxSupportedCases)
		{
			Add(findings, DiagnosticLibrary.TooManyCases, declarationLocation, displayName, builder.Count);

			return null;
		}

		if (!TryValidateCaseConversions(compilation, builder, exposedTypes, displayName, declarationLocation, findings))
			return null;

		// The builder is already in declaration order, so the tag values are correct.
		return builder.ToImmutable();
	}

	static bool TryValidateCaseConversions(
		Compilation compilation,
		ImmutableArray<UnionCaseModel>.Builder cases,
		List<ITypeSymbol> exposedTypes,
		string displayName,
		Location declarationLocation,
		ImmutableArray<ReportableDiagnostic>.Builder findings
	)
	{
		if (compilation is not CSharpCompilation csharpCompilation)
			return true;

		var valid = true;

		for (var index = 0; index < exposedTypes.Count; index++)
		{
			for (var other = index + 1; other < exposedTypes.Count; other++)
			{
				if (
					!csharpCompilation.ClassifyConversion(exposedTypes[index], exposedTypes[other]).IsImplicit
					&& !csharpCompilation.ClassifyConversion(exposedTypes[other], exposedTypes[index]).IsImplicit
				)
					continue;

				Add(
					findings,
					DiagnosticLibrary.AmbiguousCaseConversion,
					declarationLocation,
					cases[index].FullyQualifiedName,
					cases[other].FullyQualifiedName,
					displayName
				);
				valid = false;
			}
		}

		return valid;
	}

	static bool? TryFinalise(
		INamedTypeSymbol union,
		UnionCaseSource source,
		ImmutableArray<ITypeSymbol> declaredCases,
		ImmutableArray<ITypeSymbol> constructorCases,
		string displayName,
		Location declarationLocation,
		ImmutableArray<ReportableDiagnostic>.Builder findings
	)
	{
		var emitMembers = source != UnionCaseSource.Constructors;

		if (source != UnionCaseSource.Constructors && !constructorCases.IsDefaultOrEmpty)
		{
			if (!CaseSetsEqual(declaredCases, constructorCases))
			{
				Add(findings, DiagnosticLibrary.ExistingConstructorsConflict, declarationLocation, displayName);

				return null;
			}

			// The author already declared the creation members, so emitting them again would duplicate
			// constructors. Treat the union as hand-written.
			emitMembers = false;
		}

		if (emitMembers && GetConflictingMember(union) is { } conflict)
		{
			Add(findings, DiagnosticLibrary.ConflictingUnionMember, declarationLocation, displayName, conflict);

			return null;
		}

		if (declaredCases.Length == 1)
			Add(findings, DiagnosticLibrary.SingleCaseUnion, declarationLocation, displayName);

		return emitMembers;
	}

	/// <summary>
	/// Gets the required member the declaration already occupies, or <see langword="null"/>.
	/// </summary>
	static string? GetConflictingMember(INamedTypeSymbol union)
	{
		// A property cannot share its enclosing type's name (CS0542), so a union named Value cannot carry the
		// required Value property.
		if (string.Equals(union.Name, PropertyLibrary.ValueMemberName, StringComparison.Ordinal))
			return "the type name collides with the required Value property";

		foreach (var member in union.GetMembers())
		{
			if (member.IsImplicitlyDeclared)
				continue;

			if (!SymbolEqualityComparer.Default.Equals(member.ContainingType, union))
				continue;

			// Analyzers run on the compilation that already contains this generator's output, so the
			// generated Value/HasValue/TryGetValue members are part of the symbol. Only author-written
			// members can conflict.
			if (IsGeneratedMember(member))
				continue;

			if (
				member is IPropertySymbol
				&& (
					string.Equals(member.Name, PropertyLibrary.ValueMemberName, StringComparison.Ordinal)
					|| string.Equals(member.Name, PropertyLibrary.HasValueMemberName, StringComparison.Ordinal)
				)
			)
				return $"an existing '{member.Name}' member";

			if (
				member is IMethodSymbol { Parameters.Length: 1 } method
				&& string.Equals(method.Name, PropertyLibrary.TryGetValueMemberName, StringComparison.Ordinal)
				&& method.Parameters[0].RefKind == RefKind.Out
			)
				return "an existing TryGetValue member";
		}

		return null;
	}

	static void Add(
		ImmutableArray<ReportableDiagnostic>.Builder findings,
		DiagnosticDescriptor descriptor,
		Location? location,
		params object[] messageArgs
	) =>
		findings.Add(
			ReportableDiagnostic.Create(descriptor, DiagnosticLibrary.IsBlocking(descriptor), location, messageArgs)
		);

	static UnionAnalysisResult Blocked(ImmutableArray<ReportableDiagnostic>.Builder findings) =>
		new(UnionModel.Empty, findings.ToImmutable());

	static AttributeData? FindGenerateUnionAttribute(INamedTypeSymbol symbol)
	{
		foreach (var attribute in symbol.GetAttributes())
		{
			if (UnionsTypeLibrary.GenerateUnionAttribute.Matches(attribute.AttributeClass))
				return attribute;
		}

		return null;
	}

	static bool HasUnionAttribute(INamedTypeSymbol symbol)
	{
		foreach (var attribute in symbol.GetAttributes())
		{
			if (UnionsTypeLibrary.UnionAttribute.Matches(attribute.AttributeClass))
				return true;
		}

		// On net10.0 the source-declared [Union] does not resolve until the polyfill is added, which happens
		// after discovery. Match the written name as well so an author who opted in by hand never receives a
		// duplicate attribute application.
		foreach (var reference in symbol.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax() is not TypeDeclarationSyntax syntax)
				continue;

			foreach (var list in syntax.AttributeLists)
			{
				foreach (var attribute in list.Attributes)
				{
					var name = LastNameSegment(attribute.Name.ToString());
					if (
						string.Equals(name, "Union", StringComparison.Ordinal)
						|| string.Equals(name, PropertyLibrary.UnionAttributeName, StringComparison.Ordinal)
					)
						return true;
				}
			}
		}

		return false;
	}

	static bool IsGeneratedMember(ISymbol member)
	{
		foreach (var attribute in member.GetAttributes())
		{
			if (
				attribute.AttributeClass is { Name: "GeneratedCodeAttribute" } attributeClass
				&& attributeClass.ContainingNamespace?.ToDisplayString() == "System.CodeDom.Compiler"
			)
				return true;
		}

		return false;
	}

	static string LastNameSegment(string name)
	{
		var separator = name.LastIndexOf('.');
		var alias = name.LastIndexOf(':');

		return separator >= 0 ? name.Substring(separator + 1)
			: alias >= 0 ? name.Substring(alias + 1)
			: name;
	}

	static Location? GetLocation(AttributeData? attribute) =>
		attribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation();

	static Location GetSourceLocation(ISymbol? symbol, Location fallback)
	{
		if (symbol is not null)
		{
			foreach (var location in symbol.Locations)
			{
				if (location.IsInSource)
					return location;
			}
		}

		return fallback;
	}

	/// <summary>
	/// Reads the <c>typeof</c> case list from the attribute, in declaration order.
	/// </summary>
	static ImmutableArray<ITypeSymbol> GetAttributeCases(AttributeData? attribute)
	{
		if (attribute is null || attribute.ConstructorArguments.Length != 1)
			return [];

		var argument = attribute.ConstructorArguments[0];
		if (argument.Kind != TypedConstantKind.Array || argument.IsNull)
			return [];

		var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(argument.Values.Length);
		foreach (var value in argument.Values)
		{
			if (value.Kind == TypedConstantKind.Type && value.Value is ITypeSymbol type)
				builder.Add(type);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Reads the case types from the union's own partial static factory signatures.
	/// </summary>
	static ImmutableArray<ITypeSymbol> GetFactoryCases(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

		foreach (var method in OrderedFactoryMethods(union, cancellationToken))
			builder.Add(method.Parameters[0].Type);

		return builder.ToImmutable();
	}

	/// <summary>
	/// Reads the partial static factory signatures to implement, in the same order as the case list.
	/// </summary>
	static EquatableArray<UnionFactoryModel> GetFactories(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		var builder = ImmutableArray.CreateBuilder<UnionFactoryModel>();

		foreach (var method in OrderedFactoryMethods(union, cancellationToken))
		{
			builder.Add(
				new UnionFactoryModel(
					method.Name,
					ToAccessibility(method.DeclaredAccessibility),
					ToLiteral(method.Parameters[0].Type)
				)
			);
		}

		return builder.ToImmutable();
	}

	static IEnumerable<IMethodSymbol> OrderedFactoryMethods(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		foreach (var member in OrderedMembers(union))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (member is not IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method)
				continue;

			if (method.Parameters[0].RefKind != RefKind.None)
				continue;

			if (!SymbolEqualityComparer.Default.Equals(method.ReturnType, union))
				continue;

			if (!IsPartialMethodDefinition(method, cancellationToken))
				continue;

			yield return method;
		}
	}

	/// <summary>
	/// Reads the case types from a nested <c>IUnionMembers</c> provider's static <c>Create</c> methods.
	/// </summary>
	static ImmutableArray<ITypeSymbol> GetProviderCases(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		var provider = GetUnionMembersProvider(union);
		if (provider is null)
			return [];

		var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

		foreach (var member in OrderedMembers(provider))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (
				member is IMethodSymbol { IsStatic: true, Parameters.Length: 1 } method
				&& string.Equals(method.Name, PropertyLibrary.UnionMembersCreateMethodName, StringComparison.Ordinal)
				&& method.Parameters[0].RefKind == RefKind.None
				&& SymbolEqualityComparer.Default.Equals(method.ReturnType, union)
			)
				builder.Add(method.Parameters[0].Type);
		}

		return builder.ToImmutable();
	}

	/// <summary>
	/// Reads the case types from existing public single-parameter constructors.
	/// </summary>
	static ImmutableArray<ITypeSymbol> GetConstructorCases(INamedTypeSymbol union, CancellationToken cancellationToken)
	{
		var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();

		foreach (var constructor in union.InstanceConstructors)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (constructor.DeclaredAccessibility != Accessibility.Public || constructor.Parameters.Length != 1)
				continue;

			if (constructor.Parameters[0].RefKind is not (RefKind.None or RefKind.In))
				continue;

			builder.Add(constructor.Parameters[0].Type);
		}

		return builder.ToImmutable();
	}

	static INamedTypeSymbol? GetUnionMembersProvider(INamedTypeSymbol union)
	{
		foreach (var member in union.GetTypeMembers(PropertyLibrary.UnionMembersInterfaceName))
		{
			if (!member.IsImplicitlyDeclared && member.TypeKind == TypeKind.Interface)
				return member;
		}

		return null;
	}

	static IEnumerable<ISymbol> OrderedMembers(INamedTypeSymbol type) =>
		type.GetMembers()
			.OrderBy(static member =>
				member.Locations.IsDefaultOrEmpty ? int.MaxValue : member.Locations[0].SourceSpan.Start
			);

	static bool CaseSetsEqual(ImmutableArray<ITypeSymbol> left, ImmutableArray<ITypeSymbol> right)
	{
		if (left.Length != right.Length)
			return false;

		HashSet<string> leftNames = [with(StringComparer.Ordinal)];
		foreach (var type in left)
		{
			if (!TryNormalizeCase(type, out var exposed, out _, out _, out _))
				return false;

			leftNames.Add(exposed.ToDisplayString(DisplayFormat));
		}

		foreach (var type in right)
		{
			if (!TryNormalizeCase(type, out var exposed, out _, out _, out _))
				return false;

			if (!leftNames.Remove(exposed.ToDisplayString(DisplayFormat)))
				return false;
		}

		return leftNames.Count == 0;
	}

	/// <summary>
	/// Normalises a case symbol into its exposed type, creation parameter type and storage type.
	/// </summary>
	/// <remarks>
	/// A nullable value type is exposed through its underlying type, as the union pattern specifies:
	/// <c>int?</c> becomes case <c>int</c> and <c>TryGetValue(out int)</c>, while the constructor keeps
	/// <c>int?</c>. A nullable reference annotation is not part of a case's identity.
	/// </remarks>
	internal static bool TryNormalizeCase(
		ITypeSymbol symbol,
		out ITypeSymbol exposed,
		out TypeReference caseType,
		out TypeReference parameterType,
		out TypeReference storageType
	)
	{
		exposed = symbol;
		caseType = TypeReference.Empty;
		parameterType = TypeReference.Empty;
		storageType = TypeReference.Empty;

		var nullableValue = false;

		if (
			symbol is INamedTypeSymbol named
			&& named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
			&& named.TypeArguments.Length == 1
		)
		{
			exposed = named.TypeArguments[0];
			nullableValue = true;
		}
		else if (symbol.NullableAnnotation == NullableAnnotation.Annotated)
		{
			exposed = symbol.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
		}

		caseType = ToLiteral(exposed);
		parameterType = ToLiteral(
			exposed.IsReferenceType || exposed is ITypeParameterSymbol
				? symbol.WithNullableAnnotation(NullableAnnotation.Annotated)
				: symbol
		);

		if (nullableValue)
		{
			storageType = ToLiteral(exposed);
		}
		else if (exposed.IsReferenceType || exposed is ITypeParameterSymbol)
		{
			storageType = ToLiteral(exposed.WithNullableAnnotation(NullableAnnotation.Annotated));
		}
		else
		{
			storageType = ToLiteral(exposed);
		}

		return true;
	}

	/// <summary>
	/// Gets why a symbol cannot be a union case, or <see langword="null"/> when it can.
	/// </summary>
	static string? GetCaseTypeProblem(ITypeSymbol type)
	{
		if (type.TypeKind == TypeKind.Error)
			return "the type could not be resolved";

		if (type.TypeKind == TypeKind.Pointer)
			return "a pointer cannot be stored in a union";

		if (type.TypeKind == TypeKind.FunctionPointer)
			return "a function pointer cannot be stored in a union";

		if (type.TypeKind == TypeKind.Dynamic)
			return "dynamic cannot be a case; use object instead";

		if (type is INamedTypeSymbol named)
		{
			if (named.SpecialType == SpecialType.System_Void)
				return "void cannot be a case";

			if (named.IsStatic)
				return "a static class cannot be instantiated";

			if (named.IsRefLikeType)
				return "a ref struct cannot be stored in a union field";

			if (named.IsUnboundGenericType)
				return "an open generic definition cannot be a case; supply its type arguments";
		}

		return TypeReference.TryCreate(type, out _) || type is ITypeParameterSymbol
			? null
			: "the type cannot be represented in generated code";
	}

	/// <summary>
	/// Determines whether a case type can be exposed by the public creation member generated for the union.
	/// </summary>
	/// <remarks>
	/// The generated constructor is public, but its effective accessibility is capped by the union and its
	/// containing types. A public union therefore needs public case types, while an internal or private union
	/// can use a case type that is at least as accessible as the union and reachable from its declaration.
	/// This is what lets an internal union use internal case types without a warning.
	/// </remarks>
	static bool IsCaseAccessible(ITypeSymbol caseType, INamedTypeSymbol union, Compilation compilation)
	{
		if (caseType is ITypeParameterSymbol)
			return true;

		if (!compilation.IsSymbolAccessibleWithin(caseType, union))
			return false;

		// The effective accessibility rank is conservative for the protected forms:
		// a protected or protected-internal union requires a public case type, which is always safe.
		return EffectiveAccessibilityRank(caseType) >= EffectiveAccessibilityRank(union);
	}

	/// <summary>
	/// Computes the effective accessibility of a type as a rank.
	/// </summary>
	/// <remarks>
	/// Accessibility domains are not a total order (protected and internal are incomparable), but the rank
	/// is only used to decide whether a case type is at least as accessible as the union. It is conservative
	/// for the protected forms: a protected or protected-internal union requires a public case type, which
	/// is always safe.
	/// </remarks>
	static int EffectiveAccessibilityRank(ITypeSymbol type)
	{
		switch (type)
		{
			case ITypeParameterSymbol:
				return AccessibilityRank(Accessibility.Public);

			case IArrayTypeSymbol array:
				return EffectiveAccessibilityRank(array.ElementType);

			case INamedTypeSymbol named:
			{
				if (named.IsFileLocal || named.IsUnboundGenericType)
					return 0;

				var rank = AccessibilityRank(named.DeclaredAccessibility);

				for (var current = named.ContainingType; current is not null; current = current.ContainingType)
					rank = Math.Min(rank, AccessibilityRank(current.DeclaredAccessibility));

				foreach (var argument in named.TypeArguments)
					rank = Math.Min(rank, EffectiveAccessibilityRank(argument));

				return rank;
			}

			default:
				return 0;
		}
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static int AccessibilityRank(Accessibility accessibility) =>
		accessibility switch
		{
			Accessibility.Public => 6,
			Accessibility.ProtectedOrInternal => 5,
			Accessibility.Protected => 4,
			Accessibility.Internal => 3,
			Accessibility.ProtectedAndInternal => 2,
			Accessibility.Private => 1,
			_ => 0,
		};

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0072:Add missing cases")]
	static TypeDeclarationKind? GetDeclarationKind(INamedTypeSymbol symbol) =>
		symbol.TypeKind switch
		{
			TypeKind.Struct => symbol.IsRecord ? TypeDeclarationKind.RecordStruct : TypeDeclarationKind.Struct,
			TypeKind.Class => symbol.IsRecord ? TypeDeclarationKind.RecordClass : TypeDeclarationKind.Class,
			_ => null,
		};

	static TypeDeclarationAccessibility ToAccessibility(Accessibility accessibility) =>
		accessibility switch
		{
			Accessibility.Public => TypeDeclarationAccessibility.Public,
			Accessibility.Internal => TypeDeclarationAccessibility.Internal,
			Accessibility.Private => TypeDeclarationAccessibility.Private,
			Accessibility.Protected => TypeDeclarationAccessibility.Protected,
			Accessibility.ProtectedOrInternal => TypeDeclarationAccessibility.ProtectedInternal,
			Accessibility.ProtectedAndInternal => TypeDeclarationAccessibility.PrivateProtected,
			Accessibility.NotApplicable => TypeDeclarationAccessibility.Internal,
			_ => TypeDeclarationAccessibility.Internal,
		};

	static bool IsPartial(INamedTypeSymbol symbol, CancellationToken cancellationToken)
	{
		var found = false;

		foreach (var reference in symbol.DeclaringSyntaxReferences)
		{
			if (reference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax syntax)
				return false;

			if (!syntax.Modifiers.Any(SyntaxKind.PartialKeyword))
				return false;

			found = true;
		}

		return found;
	}

	static bool IsPartialMethodDefinition(IMethodSymbol method, CancellationToken cancellationToken)
	{
		foreach (var reference in method.DeclaringSyntaxReferences)
		{
			if (
				reference.GetSyntax(cancellationToken) is MethodDeclarationSyntax syntax
				&& syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
				&& syntax.Body is null
				&& syntax.ExpressionBody is null
			)
				return true;
		}

		return false;
	}

	static EquatableArray<TypeParameterModel> CreateTypeParameters(INamedTypeSymbol union)
	{
		if (union.TypeParameters.IsDefaultOrEmpty)
			return EquatableArray<TypeParameterModel>.Empty;

		var builder = ImmutableArray.CreateBuilder<TypeParameterModel>(union.TypeParameters.Length);
		foreach (var parameter in union.TypeParameters)
		{
			var constraints = ImmutableArray.CreateBuilder<string>();

			if (parameter.HasReferenceTypeConstraint)
				constraints.Add(
					parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
						? "class?"
						: "class"
				);
			else if (parameter.HasUnmanagedTypeConstraint)
				constraints.Add("unmanaged");
			else if (parameter.HasValueTypeConstraint)
				constraints.Add("struct");
			else if (parameter.HasNotNullConstraint)
				constraints.Add("notnull");

			foreach (var constraint in parameter.ConstraintTypes)
				constraints.Add(constraint.ToDisplayString(DisplayFormat));

			if (parameter.HasConstructorConstraint)
				constraints.Add("new()");

			builder.Add(new TypeParameterModel(parameter.Name, constraints.ToImmutable()));
		}

		return builder.ToImmutable();
	}

	static string CreateHintName(
		INamedTypeSymbol union,
		string namespaceName,
		ImmutableArray<ContainingTypeModel> containingTypes
	)
	{
		StringBuilder builder = new();

		if (namespaceName.Length > 0)
			builder.Append(namespaceName).Append('.');

		foreach (var containingType in containingTypes)
			builder.Append(containingType.Name).Append('.');

		builder.Append(union.Name);
		if (union.Arity > 0)
			builder.Append('_').Append(union.Arity);

		builder.Append(PropertyLibrary.GeneratedFileSuffix);

		return builder.ToString();
	}

	static UnionSourceLocation ToSourceLocation(Location location) =>
		location.SourceTree is null
			? UnionSourceLocation.None
			: new UnionSourceLocation(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);

	/// <summary>
	/// The case list chosen from one of the declaration surfaces.
	/// </summary>
	readonly record struct CaseResolution(
		UnionCaseSource Source,
		ImmutableArray<ITypeSymbol> Cases,
		EquatableArray<UnionFactoryModel> Factories,
		ImmutableArray<ITypeSymbol> ConstructorCases
	) { }

	/// <summary>
	/// The validated declaration shape.
	/// </summary>
	readonly record struct DeclarationValidation(
		TypeDeclarationKind Kind,
		ImmutableArray<ContainingTypeModel> ContainingTypes
	) { }
}
