//a simple class provider for the graph API - you can manage graph clients by just passing in values
//and then store them on whatever other code you write
//https://docs.microsoft.com/en-us/graph/best-practices-concept
//https://docs.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=CS
using System;
using System.Security;
using System.Collections.Generic; //for lists object
using System.Threading; //for CancellationToken
using System.Threading.Tasks; //for Task

//azure/microsoft api's
using Microsoft.Graph;
using Microsoft.Extensions; //tokencredentialoptions
using Azure.Identity;
using Microsoft.Identity.Client; //for workflow control

public class GraphClientProvider
{
    private GraphServiceClient graphClient;
    public GraphServiceClient GetGraphServiceClient()
    {
        return graphClient;
    }

    //https://docs.microsoft.com/en-us/azure/active-directory/develop/msal-authentication-flows
    //this is used to sign in a user to a graph API - it will prompt for MFA if enabled
    public bool ConnectGraphServiceClient(List<string> scopes, string tenantID, string clientID)
    {
        var options = new TokenCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
        };

        Func<DeviceCodeInfo, CancellationToken, Task> callback = (code, cancellation) => {
            return Task.FromResult(0);
        };

        var deviceCodeCredential = new DeviceCodeCredential(
            callback, tenantID, clientID, options);
        
        graphClient = new GraphServiceClient(deviceCodeCredential, scopes);
        return true;
    }
    
    //MAY NOT BE SECURE:
    //this is used ONLY by a background service workers to access the graph API
    //it acquires a token from an async method implemented here:
    //https://docs.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=CS#integrated-windows-provider
    //https://docs.microsoft.com/en-us/azure/active-directory/external-identities/direct-federation
    //https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-v2-netcore-daemon
    //https://docs.microsoft.com/en-us/samples/azure-samples/active-directory-dotnetcore-console-up-v2/aad-username-password-graph/
    public bool ConnectServiceWorkerToGraphClient(string username, SecureString password, List<string> scopes, string tenantID, string clientID)
    {
        //OPTIONS BELOW IS FOR FEDERATED USERS TO CLIENT
        /*
        var pca = PublicClientApplicationBuilder
            .Create(clientID)
            .WithTenantId(tenantID)
            .Build();

        var authProvider = new DelegateAuthenticationProvider(async (request) => {
            var result = await pca.AcquireTokenByIntegratedWindowsAuth(scopes).ExecuteAsync();

            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
        });
        graphClient = new GraphServiceClient(authProvider);
        return true;
        */

        //OPTIONS BELOW IS FOR USERNAME/PASSWORD authorization to client
        var pca = PublicClientApplicationBuilder
            .Create(clientID)
            .WithTenantId(tenantID)
            .Build();

        var authProvider = new DelegateAuthenticationProvider(async (request) => {
            var result = await pca.AcquireTokenByUsernamePassword(scopes, username, password).ExecuteAsync();

            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
        });
        graphClient = new GraphServiceClient(authProvider);
        return true;
    }

    //used to get user object from AAD
    public async Task<User> GetUser(string UPN)
    {
        //UPN = User Principal Name in AD
        var user = await graphClient.Users[UPN].Request().GetAsync();
        return user;
    }

    //NOTE this only works for distribution groups, not security mail groups.
    //See ./AddUserToAADGroup.ps1 for adding users to securtiy mail groups
    //assigns all passed Microsoft 365 groups to the specified user
    public async Task<bool> AssignMicrosoftGroupsToUser(User user, List<string> groups)
    {
        if(groups != new List<string>() && user != null){
            foreach(string group in groups){
                await graphClient.Groups[group].Members.References
                    .Request()
                    .AddAsync(user);
            }
            return true;
        }
        return false;
    }

    //we populate this from the CheckLicenseAmount JSON object returned
    //so it can be re-used later by an external call (e.g: if you want to reference license data later, store this
    //LicenseDataJSON tmp = CheckLicenseAmount("XXXXXX-XXXXXX-XXX-XXXX");
    //Console.WriteLine("Available units: " + tmp.AvailableUnits.ToString());
    public class LicenseDataJSON
    {
        public string LicenseSkuId{get; set;}
        public int? ConsumedUnits{get; set;}
        public int? AvailableUnits{get; set;}
        public Dictionary<string, object> AdditionalData{get; set;}
        public string CapabilityStatus{get; set;}
        public string AppliesTo{get; set;}
	public int LicensesLeft{get; set;}
    }

    //gets data available from license
    //https://docs.microsoft.com/en-us/graph/api/subscribedsku-list?view=graph-rest-1.0&tabs=http
    public async Task<LicenseDataJSON> CheckLicenseAmount(string licenseSkuName)
    {
        var subscribedSku = await graphClient.SubscribedSkus[licenseSkuName].Request().GetAsync();
        LicenseDataJSON tmpJSON = new LicenseDataJSON();
        tmpJSON.AvailableUnits = subscribedSku.PrepaidUnits.Enabled;
        tmpJSON.ConsumedUnits = subscribedSku.ConsumedUnits;
        tmpJSON.LicenseSkuId = subscribedSku.SkuId.ToString();
        tmpJSON.AdditionalData = new Dictionary<string, object>(subscribedSku.AdditionalData);
        tmpJSON.CapabilityStatus = subscribedSku.CapabilityStatus.ToString();
        tmpJSON.AppliesTo = subscribedSku.AppliesTo;

        //check if values exist and return them
        if(tmpJSON.AvailableUnits.HasValue && tmpJSON.ConsumedUnits.HasValue){
            tmpJSON.LicensesLeft = tmpJSON.AvailableUnits.Value - tmpJSON.ConsumedUnits.Value;
            return tmpJSON;
        } else{
	    tmpJSON.LicensesLeft = 0;
	}
        return 0;
    }

    //can be used to send mail as a specific user (must have the proper authorization setup to the account)
    //subject = email subject
    //body = message of the email
    //recipient = who receives it
    public async Task<bool> SendMail(string subject, string body, string recipient)
    {
        var email = new Message
        {
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = body
            },
            ToRecipients = new List<Recipient>()
            {
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = recipient
                    }
                }
            }
        };

	//change to true if you want to save the email sent
        var saveToSentItems = false;

        await graphClient.Me
            .SendMail(email, saveToSentItems)
            .Request()
            .PostAsync();

        return true;
    }

    //FORMAT of group GUID ---- XXXX-XXXX-XXXX-XXXXXXXXXX: 
    //given a list of group-id's, assign a user instance to each group
    public async Task<bool> AssignGroupToUser(string userUPN, List<string> groups)
    {
        var user = await GetUser(userUPN);
	if(user == null){
		return false;
	}
        foreach(string groupName in groups){
            await graphClient.Groups[groupName].Members.References
                .Request()
                .AddAsync(user);
        }
        return true;
    }

    //used to assign a license to a user in AAD
    //https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference
    public async Task<bool> AssignLicenseToUser(string licenseGuid, string userUPN)
    {
        //convert the string license to a guid
        Guid licenseSkuId = Guid.Parse(licenseGuid);
        var licensesToAdd = new List<AssignedLicense>();
        var licensesToRemove = new List<Guid>();

        var license = new AssignedLicense()
        {
            SkuId = licenseSkuId
        };
        licensesToAdd.Add(license);

        var removeLicenses = new List<Guid>()
        {
	        Guid.Parse("bea13e0c-3828-4daa-a392-28af7ff61a0f")
        };

        //get the userID bassed off their UPN
        var user = graphClient.Users[userUPN].Request().GetAsync();
        var oUser = user.Result;
	if(oUser == null){
		return false;
	}
        string userID = oUser.Id;

        await graphClient.Users[userID].AssignLicense(licensesToAdd, licensesToRemove)
            .Request()
            .PostAsync();

        return true;
    }

}
