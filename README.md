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
<h3>ManageADUser.cs</h3>
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

<h2>TODO:</h2>
<ul>
  <li>Allow deletion of AD user instance given a UPN</li>
  <li>Allow removal of licenses via graphprovider</li>
  <li><strong>Make AD calls linux compatible</strong> :computer:</li>
</ul>


<h2>Add User to Mail-Enabled Security Group via Powershell:</h2>
<p>Install the <strong>ExchangeOnlineManagement</strong> module.</p>
<p>Open powershell <code>cd</code> into the directory where the file is saved and run:</p>

```powershell
./AddUserToAADGroup.ps1 -LoginEmail admin@company.com -UserUPN userToAdd@company.com -Groups Group1, Group2, Group3
```

<p> If the user running the script already has admin access and is logged in currently (admin@company.com is running it as themselves) then no prompt will appear. If admin@company.com is trying to run the script as otherAdmin@company.com then a login+MFA pop-up will appear and prompt the user to sign in.</p>
<p>The script will return <code>$true</code> on success while adding the user to each group or <code>$false</code> otherwise.</p>

<h2>Adding User to AAD Groups and Assigning licenses via the Graph API</h2>
<p>Refer to <code>GraphClientProvider.cs</code> for all required libraries.</p>

```csharp
//Assuming we want to give our AAD user "john@company.com" an AAD Premium P2 license
//and also add them into our mail AAD groups Group1 + Group2
string UPN = "john@company.com";
string licenseGUID = "eec0eb4f-6444-4f95-aba0-50c24d67f998";
List<string> groups = new List<string>{"Group1", "Group2"};
List<scope> scopes = new List<string>{"User.Read"};
string tenantID = "myTenantID";
string clientID = "myClientID";

GraphClientProvider graph = new GraphClientProvider();
//use UnsecuredConnect if MFA is not enabled(not recommended)
bool connected = graph.Connect(scopes, tenantID, clientID);
if(connected){
    var licenseData = graph.CheckLicenseAmount(licenseGUID);
    if(licenseData.LicensesLeft > 0){
        List<string> licensesToAssign = new List<string>{licenseGUID};
        //assign them the license
        graph.AssignLicenseToUser(licensesToAssign, UPN);
        //add them to groups
        graph.AssignGroupToUser(UPN, groups);
        //john@company.com now has a AAD premium P2 license and is in Group1 + Group2
    }
}
```


<h2>Creating Users in Local AD instance(only on Windows)</h2>
<p>See <code>ManageADUser.cs</code> for the libraries required.</p>

```csharp
//Create a user called "John Smith" with the username "john" and e-mail address+UPN "john@company.com"
//inside of OU: Company.COM/America/Washington/Sales/Users
ActiveDirectoryUser adUser = new ActiveDirectoryUser();
bool connected = adUser.Connect("Admin", "Password", "Company", "COM");
if(connected){
    List<string> myOU = new List<string>{"America", "Washington", "Sales", "Users"};
    Dictionary<string,string> param; = new Dictionary<string,string>{
        {"UPN", "john@company.com"},
        {"Email", "john@company.com"},
        {"Username", "john"},
        {"FirstName", "John"},
        {"LastName", "Smith"}
    };
    //points internal directory to correct entry
    adUser.ChangeOU(myOU);
    bool success = adUser.LazyCreateADUser(params);
    if(success){
      //John Smith is now a user in your OU. Handle success from here
    } else {
      //An error occurred, you can get the error message to see what went wrong
      Console.WriteLine(adUser.GetExceptionMessage());
    }
}
//connection failed - print error to find out why
Console.WriteLine(adUser.GetExceptionMessage());
```

<p>All code is written under the MIT license. Use at your own discretion.</p>
