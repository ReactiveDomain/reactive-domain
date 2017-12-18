using System;
using System.Reactive;
using ReactiveUI;

namespace ReactiveDomain.ViewObjects
{
    public static class Interactions
    {
        public static readonly Interaction<UserError, Unit> Errors = new Interaction<UserError, Unit>();
    }
}
