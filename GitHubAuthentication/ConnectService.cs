using System;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.Alm.Authentication;
using Microsoft.VisualStudio.Shell;
using Octokit.GraphQL.Core.Deserializers;

namespace GitHubAuthentication
{
    public class ConnectService
    {
        // This is the connect command from GitHub for Visual Studio
        static readonly Guid guidGitHubCmdSet = new Guid("c4c91892-8881-4588-a5d9-b41e8f540f5a");
        const int addConnectionCommand = 272;

        readonly IServiceProvider serviceProvider;

        public ConnectService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public TResult EnsureConnection<TResult>(Func<string, TResult> method)
        {
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

            var command = FindCommand(guidGitHubCmdSet, addConnectionCommand);
            if (command == null)
            {
                throw new InvalidOperationException("GitHub for Visual Studio isn't installed");
            }

            DTE.Commands.Raise(command.Guid, command.ID, null, null);
        }

        Command FindCommand(Guid guid, int id)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                return DTE.Commands.Item(guid, id);
            }
            catch (COMException)
            {
                return null;
            }
        }

        DTE DTE
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                return serviceProvider.GetService(typeof(DTE)) as DTE;
            }
        }

    }
}
