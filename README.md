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
  <li>Refer to <code>GraphClientProvider.cs</code> for all required libraries.</li>
  <li>Copy <code>GraphClientProvider.cs</code> to your project and set the namespace.</li>
  <li>From your code you can create an instance of GraphClientProvider with <code>GraphClientProvider _graphProvider = new GraphClientProvider();</code></li>
  <li>You can connect the graph instance by passing along your scopes, tenantID and clientID from your app into <code>_graphProvider.Connect()</code></li>
  <li>Everything in this instance will return <code>true</code> if the command ran successfully or <code>false</code> otherwise. So you can save the results and perform your next functions <code>CheckLicenseAmount(), SendMail(), AssignLicenseToUser(), etc</code> as needed.
</ol>


<h2>Creating Users in Local AD instance(only on Windows)</h2>
<ol>
  <li>Refer to <code>ManageADUser.cs</code> for all required libraries.</li>
  <li>Copy <code>ManageADUser.cs</code> to your project and set the namespace.</li>
  <li>From any point in your project create an instance of the ADUser with <code>ActiveDirectoryUser adUser = new ActiveDirectoryUser()</code></li>
  <li>Create a <code>Dictionary<<string,>string,string<string>> param</code> with all the values you'll want to add into the user. There are a few that are required to create a user. Please reference the source code for the entire list. The <strong>required parameters</strong> are: <code>UPN, Email, Username, FirstName, LastName</code></li>
  <li>First connect to the AD instance by calling <code>adUser.Connect("Admin", "Password", "DomainName", "TLD");</code></li>
  <li>If this evaluates to false - double check that your passing the correct parameters or get the ExceptionMessage with <code>adUser.GetExceptionMessage()</code>. If it evaluates to true - assuming you want to create an AD user called "John Smith" with the email address "john@company.com" and username "john". You would pass along the following: 
    <br>
    <code>
      Dictionary<<string,>string,string<string>> param = new Dictionary<<string,>string,string<string>>
      {</code>
      <br>
      <code>
        {"UPN", "john@company.com"},</code>
      <br>
        <code>{"Email", "john@company.com"},</code>
      <br>
        <code>{"Username", "john"},</code>
      <br>
        <code>{"FirstName", "John"},</code>
      <br>
        <code>{"LastName", "Smith"}};</code>
    </code>
    <br>
    then you would call:
    <br>
      <code>adUser.LazyCreateADUser(param);</code> which will evaluate all parameters passed and create the user, set the password and update all tab information available. Alternatively if you want more control you can use <code>adUser.CreateADUser(param);</code> and then manually update each tab by calling <code>UpdateGeneral(), UpdateProfile(), etc</code>
  </li>
  <li>All methods in this class return <code>true</code> or <code>false</code> depending on their values.</li>
</ol>

<p>All code is written under the MIT license. Use at your own discretion.</p>
