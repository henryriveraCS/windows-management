# windows-management
<h1>WORK IN PROGRESS</h1>
<p>Some scripts for quick and dirty management of users/licenses in AD+Azure AD.</p>
<h2>Current Features:</h2>
<h3>GraphClientProvider.cs</h3>
<ul>
  <li>Login to the graph client using either MFA (for account users) or simple Username/Password (if running as a service or MFA not enabled)</li>
  <li>Assign license to user with just their email address + license GUID</li>
  <li>Add user to Azure AD groups using Graph API (and you can use powershell for mail-enabled security groups since the API doesn't currently support it)</li>
  <li>Get information about a license(total licenses bought, licenses left, SkuID, etc)</li>
  <li>Send mail as the user</li>
</ul>
<h3>CreateADUser.cs</h3>
<ul>
  <li>Create users in a specified AD OU</li>
  <li>Populate the AD User information <code>General, Account, Member of, Profile, etc</code> while creating the user(will be separated into a separate function soon)</li>
  <li>Generate a cryptographically secure password for a user.</li>
</ul>
<h3>AddUserToAADGroup.ps1</h3>
<ul>
  <li>Add user to mail-enabled security groups.</li>
</ul>
<br>
<p>In order to use GraphClientProvider.cs you'll need: <code>Microsoft.Graph, Azure.Identity and Microsoft.Identity.Client</code><p>

In order to use CreateADUser.cs you'll need:
<code>System.DirectoryServices, System.DirectoryServices.AccountManagement</code>

In order to use the powershell scripts you'll need:
<code>ExchangeOnlineManagement (Exchange Online Powershell V2 Module)</code>

<h2>Add User to Distribution Group via Powershell:</h2>
<ol>
  <li>Install the <strong>ExchangeOnlineManagement</strong> module.</li>
  <li>Save <code>AddUserToAADGroup.ps1</code> onto any local directory/folder.</li>
  <li>Then add users into a distribution group by using: <code>./AddUserToAADGroup.ps1 -LoginEmail admin@company.com -UserUPN userToAdd@company.com -Groups Group1, Group2, Group3</code></li>
</ol>
<p> If the user running the script already has admin access and is logged in currently (admin@company.com is running it as themselves) then no prompt will appear. If admin@company.com is trying to run the script as otherAdmin@company.com then a login+MFA pop-up will appear and prompt the user to sign in.</p>
<p>The script will return <code>$true</code> on success while adding the user to each group or <code>$false</code> otherwise.</p>



<h2>Adding User to Azure Active Directory Groups via the Graph API</h2>
<ol>
  <li>Make sure you have the following libraries:
    <br>
    <code>Microsoft.Graph</code>(the graph API itself)
    <br>
    <code>Azure.Identity</code>(token handling for Azure)
    <br>
    <code>Microsoft.Identity.Client</code>(used for creating a public facing client to Azure)
  </li>
  <li>Copy GraphClientProvider.cs over to your project and make sure it's in the correct namespace.</li>
  <li>From your code you can create an instance of GraphClientProvider with <code>GraphClientProvider _graphProvider = new GraphClientProvider();</code></li>
  <li>You can connect the graph instance by passing along your scopes, tenantID and clientID from your app into <code>_graphProvider.ConnectGraphServiceClient()</code></li>
  <li>Everything in this instance will return <code>true</code> if the command ran successfully or <code>false</code> otherwise. So you can save the results and perform your next functions <code>CheckLicenseAmount(), SendMail(), AssignLicenseToUser(), etc</code> as needed.
</ol>


<p>Feel free to contact me for report any bugs or issues you encounter(or just raise an issue to this repo)</p>
