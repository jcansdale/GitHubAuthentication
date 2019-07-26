using System;
using EnvDTE;
using Microsoft;
using Microsoft.Alm.Authentication;
using Microsoft.VisualStudio.Shell;
using Octokit.GraphQL.Core.Deserializers;

namespace GitHubAuthentication
{
    public class ConnectService
    {
        // Connect command from GitHub for Visual Studio
        static readonly CommandInfo gitHubConnectCommand = new CommandInfo { Guid = "c4c91892-8881-4588-a5d9-b41e8f540f5a", ID = 0x0110 };

        // Connect command from GitHub Essentials
        static readonly CommandInfo essentialsConnectCommand = new CommandInfo { Guid = "8de10943-8643-4f81-88a3-83b81d204ff4", ID = 0x0110 };

        readonly IServiceProvider serviceProvider;

        public ConnectService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public TResult EnsureConnection<TResult>(Func<string, TResult> method)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var token = FindGitHubToken();
            if (token == null)
            {
                // Show the connect dialog if the user isn't authenticated
                ShowConnectDialog();
                token = FindGitHubToken();
                if(token == null)
                {
                    throw new ApplicationException("Couldn't establish a GitHub connection");
                }
            }

            try
            {
                return method(token);
            }
            catch(ResponseDeserializerException)
            {
                // Show the connect dialog if token doesn't have the required scope
                // or if SAML enforcement is enabled
                ShowConnectDialog();
                var newToken = FindGitHubToken();
                if(newToken == null || newToken == token)
                {
                    throw;
                }

                return method(newToken);
            }
        }

        public string FindGitHubToken()
        {
            var secrets = new SecretStore("git");
            var auth = new BasicAuthentication(secrets);
            var creds = auth.GetCredentials(new TargetUri("https://github.com"));
            return creds?.Password;
        }

        public void ShowConnectDialog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (TryRaiseCommand(essentialsConnectCommand))
            {
                return;
            }

            if (TryRaiseCommand(gitHubConnectCommand))
            {
                return;
            }

            throw new InvalidOperationException("Couldn't find GitHub for Visual Studio or Essentials extension");
        }

        bool TryRaiseCommand(CommandInfo commandInfo)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var dte = serviceProvider.GetService(typeof(DTE)) as DTE;
                Assumes.Present(dte);
                dte.Commands.Raise(commandInfo.Guid, commandInfo.ID, null, null);
                return true;
            }
            catch (ArgumentException)
            {
                // Command not found
                return false;
            }
        }

        struct CommandInfo
        {
            public string Guid;
            public int ID;
        }

    }
}
