// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;

namespace IdentityServerHost.Quickstart.UI
{
    public class AccountOptions
    {
        public static bool AllowLocalLogin = true;
        public static bool AllowRememberLogin = false;
        public static bool DisplayDomainDropDown = true;
        public static TimeSpan RememberMeLoginDuration = TimeSpan.FromDays(30);

        public static bool ShowLogoutPrompt = false;
        public static bool AutomaticRedirectAfterSignOut = true;
        // specify the Windows authentication scheme being used
        public static readonly string WindowsAuthenticationSchemeName = Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme;
        // if user uses windows auth, should we load the groups from windows
        public static bool IncludeWindowsGroups = false;
        public static string DefaultIdentityProvider = "AD";

        public const string InvalidCredentialsErrorMessage = "Invalid username or password";
        public const string UserAccountIsLockedErrorMessage = "User account is locked";
        public const string UserAccountIsDisabledErrorMessage = "User account is disabled";
        public const string UserAuthenticated = "User Authenticated";
        public const string UserCancelledMessage = "User Cancelled";
    }
}
