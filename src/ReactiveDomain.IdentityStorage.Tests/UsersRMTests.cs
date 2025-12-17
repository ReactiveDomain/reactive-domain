using System;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.IdentityStorage.ReadModels;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.IdentityStorage.Tests;

[Collection("UserDomainTests")]
public sealed class UsersRMTests : ReadModelTestSpecification, IDisposable {
    private readonly UsersRm _rm;

    private readonly Guid _id1 = Guid.NewGuid();
    private readonly Guid _id2 = Guid.NewGuid();
    private readonly string _subjectId = Guid.NewGuid().ToString();
    private readonly string _subjectId2 = Guid.NewGuid().ToString();
    private const string AuthProvider = "AD";
    private const string AuthDomain = "CompanyNet";
    private const string UserName = "jsmith";
    private const string UserName2 = "jsmith2";
    private const string GivenName = "John";
    private const string Surname = "Smith";
    private const string FullName = "John Smith";
    private const string Email = "john.smith@Company1.com";

    public UsersRMTests() {
        _rm = new UsersRm(Connection);
        AddUsers();
    }

    [Fact]
    public void correct_users_exist() {
        AssertEx.IsOrBecomesTrue(() => _rm.UsersById.ContainsKey(_id1));
        AssertEx.IsOrBecomesTrue(() => _rm.HasUser(_subjectId, AuthDomain, out _));
        AssertEx.IsOrBecomesTrue(() => _rm.UsersById.ContainsKey(_id2));
        AssertEx.IsOrBecomesTrue(() => _rm.HasUser(_subjectId2, AuthDomain, out _));

        Assert.False(_rm.UsersById.ContainsKey(Guid.NewGuid()));
        Assert.False(_rm.UsersById.ContainsKey(Guid.Empty));
        Assert.False(_rm.HasUser(AuthDomain, "bogus", out _));
        Assert.False(_rm.HasUser("bogus", _subjectId, out _));
    }

    [Fact]
    public void can_get_user_id_by_SID() {
        var id1 = Guid.Empty;
        var id2 = Guid.Empty;
        AssertEx.IsOrBecomesTrue(() => _rm.HasUser(_subjectId, AuthDomain, out id1));
        Assert.Equal(_id1, id1);
        AssertEx.IsOrBecomesTrue(() => _rm.HasUser(_subjectId2, AuthDomain, out id2));
        Assert.Equal(_id2, id2);
    }


    [Fact]
    public void cannot_get_nonexistent_user() {
        Assert.False(_rm.HasUser(_subjectId, "bogus", out _));
        Assert.False(_rm.HasUser("bogus", AuthDomain, out _));
    }

    private void AddUsers() {
        var evt1 = MessageBuilder.New(
            () => new UserMsgs.UserCreated(
                _id1));
        var evt2 = MessageBuilder.New(
            () => new UserMsgs.UserDetailsUpdated(
                _id1,
                FullName,
                GivenName,
                Surname,
                Email));
        var evt3 = MessageBuilder.New(
            () => new UserMsgs.AuthDomainMapped(
                _id1,
                _subjectId,
                AuthProvider,
                AuthDomain,
                UserName));

        var evt4 = MessageBuilder.New(
            () => new UserMsgs.UserCreated(
                _id2));
        var evt5 = MessageBuilder.New(
            () => new UserMsgs.UserDetailsUpdated(
                _id2,
                FullName,
                GivenName,
                Surname,
                Email));
        var evt6 = MessageBuilder.New(
            () => new UserMsgs.AuthDomainMapped(
                _id2,
                _subjectId2,
                AuthProvider,
                AuthDomain,
                UserName2));

        _rm.DirectApply(evt1);
        _rm.DirectApply(evt2);
        _rm.DirectApply(evt3);
        _rm.DirectApply(evt4);
        _rm.DirectApply(evt5);
        _rm.DirectApply(evt6);
    }
    public void Dispose() {
        _rm?.Dispose();
    }
}