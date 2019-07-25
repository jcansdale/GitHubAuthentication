using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Octokit.GraphQL;
using Task = System.Threading.Tasks.Task;

namespace GitHubAuthentication
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AuthenticateCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("e6c16b0a-4d43-4ddc-8ff1-d13892109924");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private readonly ConnectService connectService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticateCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private AuthenticateCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);

            connectService = new ConnectService(ServiceProvider);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static AuthenticateCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in AuthenticateCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new AuthenticateCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // The viewer's name should be visible to any authenticated user.
            //var message = connectService.EnsureConnection(QueryViewerNameMessage);

            // Show the name and email address of the viewer.
            // Accessing the users email requires the 'user:email' or 'read:user' scopes.
            // Git for Windows will only request the 'gist' and 'repo' scopes which isn't enough.
            //var message = connectService.EnsureConnection(QueryViewerEmailMessage);

            // The 'github' organization has SAML enforcement enabled. This means the user
            // must be authenticated using browser/webflow not user/pass/2FA. This isn't possible using
            // Git for Windows so GitHub for Visual Studuo must be used to authenticate.
            //var message = connectService.EnsureConnection(token => QueryRepositoryDescription(token, "github", "hubbers"));

            // github/VisualStudio is a public repository and is visible to any user.
            var message = connectService.EnsureConnection(token => QueryRepositoryDescription(token, "github", "VisualStudio"));

            // Show a message box to prove we were here
            VsShellUtilities.ShowMessageBox(
                this.package,
                "",
                message,
                OLEMSGICON.OLEMSGICON_INFO,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        string QueryViewerNameMessage(string token)
        {
            return package.JoinableTaskFactory.Run(async () =>
            {
                var query = new Query().Viewer.Select(v => new { v.Name }).Compile();

                var connection = new Connection(ProductInformation, token);
                var result = await connection.Run(query);

                return $@"Hello, {result.Name}!";
            });
        }

        string QueryViewerEmailMessage(string token)
        {
            return package.JoinableTaskFactory.Run(async () =>
            {
                var query = new Query().Viewer.Select(v => new { v.Name, v.Email }).Compile();

                var connection = new Connection(ProductInformation, token);
                var result = await connection.Run(query);

                return $@"Hello, {result.Name}!

Your public email address is {result.Email}.";
            });
        }

        string QueryRepositoryDescription(string token, string owner, string name)
        {
            return package.JoinableTaskFactory.Run(async () =>
            {
                var query = new Query().RepositoryOwner(owner).Repository(name).Select(v => new { v.NameWithOwner, v.Description }).Compile();

                var connection = new Connection(ProductInformation, token);
                var result = await connection.Run(query);
                if(result == null)
                {
                    return $"Viewer doesn't have access to the repository {owner}/{name}";
                }

                return $"{result.NameWithOwner}: {result.Description}";
            });
        }

        static ProductHeaderValue ProductInformation { get; } = new ProductHeaderValue("YOUR_PRODUCT_NAME", "YOUR_PRODUCT_VERSION");
    }
}
