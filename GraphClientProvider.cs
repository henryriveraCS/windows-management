//a simple class provider for the graph API - you can manage graph clients by just passing in values
//and then store them on whatever other code you write

//REFERENCES
//https://docs.microsoft.com/en-us/graph/best-practices-concept
//https://docs.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=CS
//https://docs.microsoft.com/en-us/samples/azure-samples/active-directory-dotnetcore-console-up-v2/aad-username-password-graph/

using System;
using System.Security; //for SecureString
using System.Collections.Generic; //for lists object
using System.Threading; //for CancellationToken
using System.Threading.Tasks; //for Task

//azure/microsoft api's
using Microsoft.Graph;
//using Microsoft.Extensions;
using Azure.Identity; //OAuth into Azure
using Microsoft.Identity.Client; //For PublicCLientAuthBuild

namespace SampleNamespace
{
	public class GraphClientProvider
	{
	    private GraphServiceClient graphClient;
	    public GraphServiceClient GetGraphClient(){
			return graphClient;
	    }

	    //this is used to sign in a user to a graph API - it will prompt for MFA if enabled
	    public bool Connect(List<string> Scopes, string TenantID, string ClientID){
			var options = new TokenCredentialOptions{
		    		AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
			};

			Func<DeviceCodeInfo, CancellationToken, Task> callback = (code, cancellation) => {
		    	return Task.FromResult(0);
			};

			var deviceCodeCredential = new DeviceCodeCredential(callback, TenantID, ClientID, options);
		
			graphClient = new GraphServiceClient(deviceCodeCredential, Scopes);
			return true;
		}
	    
	    //Whenever possible use ConnectGraphClient instead of this function
	    //Connects to an AAD account without MFA-enabled using username + password
	    public bool UnsecuredConnect(string Username, SecureString Password, 
									List<string> Scopes, 
									string TenantID, 
									string ClientID)
		{
			//OPTIONS BELOW IS FOR USERNAME/PASSWORD authorization to client
			var pca = PublicClientApplicationBuilder
		   	.Create(ClientID)
		    .WithTenantId(TenantID)
		    .Build();

			var authProvider = new DelegateAuthenticationProvider(async (request) => {
		    	var result = await pca.AcquireTokenByUsernamePassword(Scopes, Username, Password).ExecuteAsync();

		    	request.Headers.Authorization = 
				new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);
			});
			graphClient = new GraphServiceClient(authProvider);
			return true;
	    }

	    //used to get user object from AAD
	    public async Task<User> GetUser(string UPN){
			var user = await graphClient.Users[UPN].Request().GetAsync();
			return user;
		}

		//NOTE this only works for specific distribution groups
		//See ./AddUserToAADGroup.ps1 for adding users to mail-enabled security groups
		//assigns all passed Microsoft 365 groups to the specified user
		public async Task<bool> AssignAADGroupsToUser(User user, List<string> Groups){
			if(Groups != new List<string>() && user != null){
				foreach(string group in Groups){
					await graphClient.Groups[group].Members.References
						.Request()
						.AddAsync(user);
				}
				return true;
			}
			return false;
	    }

	    public class LicenseDataJSON{
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
	    public async Task<LicenseDataJSON> CheckLicenseAmount(string LicenseSkuName){
			var subscribedSku = await graphClient.SubscribedSkus[LicenseSkuName].Request().GetAsync();
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
			return new LicenseDataJSON();
	    }

	    //Send mail out as the logged-in graph user
	    //subject = email subject
	    //body = message of the email
	    //recipient = who receives it
	    //saveSentItems = whether or not to save it in the sent box
	    public async Task<bool> SendMail(string subject, string body, string recipient, bool saveSentItems){
			var email = new Message{
				Subject = subject,
				Body = new ItemBody
					{
					ContentType = BodyType.Text,
					Content = body
				},
				ToRecipients = new List<Recipient>(){
					new Recipient{
						EmailAddress = new EmailAddress{
							Address = recipient
						}
					}
		    }
			};

			await graphClient.Me
		    	.SendMail(email, saveSentItems)
		    	.Request()
		    	.PostAsync();

			return true;
	    }

	    //given a list of group GUID's, assign a user instance to each group
	    public async Task<bool> AssignGroupToUser(string UserUPN, List<string> Groups){
			var user = await GetUser(UserUPN);
				if(user == null){
					return false;
				}
			foreach(string groupID in Groups){
		    	await graphClient.Groups[groupID].Members.References
				.Request()
				.AddAsync(user);
			}
			return true;
	    	}

	    	//used to assign a license to a user in AAD
	    	//https://docs.microsoft.com/en-us/azure/active-directory/enterprise-users/licensing-service-plan-reference
	    public async Task<bool> AssignLicenseToUser(List<string> LicenseList, string UserUPN){
			var licensesToAdd = new List<AssignedLicense>();
			var licensesToRemove = new List<Guid>();

			foreach(string LicenseGUID in LicenseList){
				Guid licenseSkuId = Guid.Parse(LicenseGUID);
				var license = new AssignedLicense(){SkuId = licenseSkuId};
				licensesToAdd.Add(license);
			}

			var removeLicenses = new List<Guid>(){
				Guid.Parse("bea13e0c-3828-4daa-a392-28af7ff61a0f")
			};

			//get the userID bassed off their UPN
			var user = graphClient.Users[UserUPN].Request().GetAsync();
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
}
