// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "test methods", Scope = "member", Target = "~M:ReactiveDomain.Policy.Tests.PolicyMapTest.policy_map_has_correct_roles_and_permisssions")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "test methods", Scope = "member", Target = "~M:ReactiveDomain.Policy.Tests.PolicyMapTest.policy_can_be_enforced")]
[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "test methods", Scope = "member", Target = "~M:ReactiveDomain.Policy.Tests.PolicyMapTest.can_create_and_resolve_permissions_type_names")]
[assembly: SuppressMessage("Usage", "xUnit1013:Public method should be marked as test", Justification = "handle interface", Scope = "member", Target = "~M:ReactiveDomain.Policy.Tests.PolicyMapTest.Handle(ReactiveDomain.Policy.Tests.OtherMsg)")]
