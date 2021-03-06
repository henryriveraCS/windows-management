using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Cryptography;

namespace SampleNamespace
{
	[SupportedOSPlatformAttribute("windows")]
	public class ActiveDirectoryUser
	{
		//keeping track of internal variables to be used by different functions once this class is initialized
		private string _exceptionMsg = "";
		private string _stackMsg = "";
		private string _domainName = "";
		private string _tld = "";
		private string _username = "";
		private string _upn = "";
		private DirectoryEntry _baseOU = new DirectoryEntry();
		private DirectoryEntry _userOU = new DirectoryEntry();
		private DirectoryEntry _user = new DirectoryEntry();

		//used to retrieve the class name for exception messages
		protected string GetClassName(){return this.GetType().Name;}

		//if any functions are returning false and you are not sure why - use this
		public string GetExceptionMessage(){
			if(_exceptionMsg != ""){
				return _exceptionMsg;
			} else{
				return "No error exception set.\n";
			}
		}

		//Get stack messages at any point from your code
		public string GetStackMessage(){
			return _stackMsg;
		}

		//used to add values to the class stack message
		private void AddStackMessage(string StackMessage){
			_stackMsg += StackMessage + "\n";
		}
		
		/*
			To make it easier to debug exception messages a string version of the stack trace is returned alongside the error
			Format of exception messages will be:
			ERROR AT MyADClass - UpdateProfile:
			Error Message: The specified directory service attribute or value does not exist
			STACK TRACE:
			Connecting to Directory: LDAP://Company.com
			Successfully connected to directory
			Creating User Instance: John Smith
			User Instance Created
			Updating Profile
			Setting Profile Path to: MyPath
			Attempting to commit changes <--- exception occurs after this
			======================================================
			The above output means that an error occured while trying to update the profile path for the user John Smith.
			Double-check your input parameters contain the key pair "@ProfilePath":"MyPath"
			and make sure that the _user instance exists before calling UpdateProfile()
		*/
		//used during exceptions to log errors
		private void SetExceptionMessage(string MethodName, string ExceptionMessage){
			_exceptionMsg = "ERROR AT " + GetClassName() + " - " + MethodName + ":\nError Message:" + ExceptionMessage + "\nSTACK TRACE:\n" + _stackMsg;
		}

		//Set's exception message whenever UserEntry is not initiated correctly.
		private void UserEntryException(string MethodName){
 			SetExceptionMessage(MethodName,"UserOU was not set. Please run SetOU() first");
		}

		private void FullNameException(string MethodName){
			SetExceptionMessage(MethodName,
				"The full name could not be determined. Please make sure the following parameters are included: FirstName, LastName");
		}

		private void RequiredParametersException(string MethodName){
			SetExceptionMessage(MethodName,
				"Please make sure you passed in all required Parameters:UPN, Username, Email, FirstName, LastName");
		}

		//generates a random cryptographically secure password of a set-length
		//then pads it with some basic Windows password requirements (adds numbers, symbols, etc)
		private string CreateSecurePassword(int Size){
			byte[] pwBytes = new byte[Size];
			using(RandomNumberGenerator rng = RandomNumberGenerator.Create()){
						rng.GetBytes(pwBytes);
			}
			string tmpPW = System.Convert.ToBase64String(pwBytes);
			if(!tmpPW.Any(char.IsUpper)){
				tmpPW = "BCA" + tmpPW;
			}
			if(!tmpPW.Any(char.IsSymbol)){
				tmpPW = tmpPW + "!?@";
			}
			if(!tmpPW.Any(char.IsLower)){
				tmpPW = tmpPW + "c";
			}
			if(!tmpPW.Any(char.IsNumber)){
				tmpPW = tmpPW + "73781";
			}
			return tmpPW;
		}

		//sets password for user entry
		public bool SetPassword(string Password){
			if(UserExists(nameof(UpdateGeneral)) == false){
				return false;
			}
			_user.Invoke("SetPassword", Password);
			if(SaveChanges(nameof(SetPassword))){
				return true;
			}
			return false;
		}

		//check if a username is valid
		public bool UsernameIsValid(){
			if(_username == ""){
				SetExceptionMessage(nameof(UsernameIsValid), "\nUsername is not set.");
				return false;	
			}
			return true;
		}

		//check if email is valid
		public bool EmailIsValid(string Email){
			if(_upn == ""){
				SetExceptionMessage(nameof(EmailIsValid), "User Email is not set.");
				return false;
			}
			return true;
		}

		//returns an authenticated entry directory if the domain was successfully connected to
		private DirectoryEntry CreateDirectoryEntry(string Username, string Password, string DomainName, string TLD){
			string fullPath = "LDAP://" + DomainName + "." + TLD;
			AddStackMessage("Attempting to join directory: " + fullPath);
			try{
				DirectoryEntry ldapConnection = new DirectoryEntry(fullPath);
				ldapConnection.Username = Username;
				ldapConnection.Password = Password;
				ldapConnection.AuthenticationType = AuthenticationTypes.Secure;
				AddStackMessage("Successfully connected to:" + ldapConnection.Path);
				return ldapConnection;
			} catch(Exception e){
				SetExceptionMessage(nameof(CreateDirectoryEntry), e.Message);
				return new DirectoryEntry();	
			}
		}

		//will add the AD user into the passed local AD groups
		public bool AssignGroupsToUser(List<string> Groups){
			if(Groups != new List<string>()){
				try{
					using(PrincipalContext pc = new PrincipalContext(ContextType.Domain, _domainName)){
						foreach(string groupName in Groups){
							GroupPrincipal group = GroupPrincipal.FindByIdentity(pc, groupName);
							group.Members.Add(pc, IdentityType.UserPrincipalName, _upn);
							//DO NOT RUN .DELETE() OR IT WILL ITERATIVELY DELETE GROUPS - YOU'RE WELCOME
							group.Save();
						}
					}
					return true;
				}
				catch(Exception e){
					SetExceptionMessage(nameof(AssignGroupsToUser), e.Message);
					return false;
				}
			} else{
				SetExceptionMessage(nameof(AssignGroupsToUser), "Groups is empty. Please pass a List<string> of the groups you want added");
				return false;
			}
		}

		//This should always be executed first in order to properly execute other functions
		//verifies the admin credentials are valid by signing in and sets internal AD values
		public bool Connect(string Username, string Password, string DomainName, string TLD){
			AddStackMessage(nameof(Connect) + " attempting to connect.");
			_baseOU = new DirectoryEntry();
			try{
				AddStackMessage("Attempting to authenticate into: " + DomainName + "." + TLD);
				_baseOU = CreateDirectoryEntry(Username, Password, DomainName, TLD);
				if(_baseOU != new DirectoryEntry()){
					AddStackMessage("Successfully connected to Directory\nSetting internal domain and tld");
					_domainName = DomainName;
					_tld = TLD;
					return true;
				}
				SetExceptionMessage(nameof(Connect), "Failed to connect to AD. Please verify your input parameters");
				return false;
			} catch(Exception e){
				SetExceptionMessage(nameof(Connect), e.Message);
				return false;
			}
		}

		//E.G: if you want to create a user in an OU Domain.COM/America/Washington/Department/Sales/Users
		//then OUList should be a List<string> with values: ["America", "Washington", "Department", "Sales", "Users"]
		//points the internal _userEntry to the specified User OU
		public bool ChangeOU(string Username, string Password, List<string> OUList){
			int totalOUCount = OUList.Count;
			if(totalOUCount <= 0){
				SetExceptionMessage(nameof(ChangeOU), "Not enough arguments in OUList, please make sure you have at least 1 item in the list");
				return false;
			}

			DirectoryEntries _tmpOU = _baseOU.Children;
			for(int current=0; current < totalOUCount; current ++){
				AddStackMessage("SEARCHING FOR NEXT OU:" + OUList[current]);
				_userOU = _tmpOU.Find("OU=" + OUList[current], "organizationalUnit");
				_tmpOU = _userOU.Children;
			}
			return true;
		}

		//used to check that a user instance exists.
		private bool UserExists(string MethodName){
			if(_user != new DirectoryEntry() && _user != _userOU){
				AddStackMessage("User does exists. Called by: " + MethodName);
				return true;
			}
			SetExceptionMessage(MethodName, "User instance not set: " + _user.Name + "--" + _user.Path);
			return false;
		}

		//used by updateTabs() methods to add messages to stack
		//E.G : "Setting FirstName to: John", "Setting LastName to: Smith", etc
		private void AddTabMessageToStack(string DataField, string DataValue){
			AddStackMessage("Setting " + DataField + " to: " + DataValue);
		}

		//used to log + commit user changes. Will return True if commit didn't cause an exception.
		private bool SaveChanges(string MethodName){
			AddStackMessage("Atttempting to commit changes for: " + MethodName);
			try{
				_user.CommitChanges();
				AddStackMessage("Successfully commited changes for " + MethodName);
				return true;
			} catch(Exception e){
				SetExceptionMessage(MethodName, e.Message);
				return false;
			}
		}
		//WIP - WORKING ON FIXES SO EVERYTHING IS WRAPPED IN A TRY CATCH UNTIL THEN
		//updates Organization tab
		public bool UpdateOrganization(Dictionary<string, string> Parameters){
			if(UserExists(nameof(UpdateOrganization)) == false){
				return false;
			};
			foreach(var kvp in Parameters){
				if(kvp.Key == "JobTitle"){
					AddTabMessageToStack(kvp.Key, kvp.Value);
					_user.Properties["title"].Value = kvp.Value;
				}
				else if(kvp.Key == "Department"){
					AddTabMessageToStack(kvp.Key, kvp.Value);
					_user.Properties["department"].Value = kvp.Value;
				}
				else if(kvp.Key == "Company"){
					AddTabMessageToStack(kvp.Key, kvp.Value);
					_user.Properties["company"].Value = kvp.Value;
				}
				else if(kvp.Key == "Manager"){
					AddTabMessageToStack(kvp.Key, kvp.Value);
					_user.Properties["manager"].Value = kvp.Value;
				}
				else if(kvp.Key == "DirectReports"){
					AddTabMessageToStack(kvp.Key, kvp.Value);
					_user.Properties["directReports"].Value = kvp.Value;
				}
			}
			if(SaveChanges(nameof(UpdateOrganization))){
				return true;
			}
			return false;
		}

		//updates Telephone Tab
		public bool UpdateTelephone(Dictionary<string, string> Parameters){
			if(UserExists(nameof(UpdateOrganization)) == false){
				return false;
			};
			foreach(var kvp in Parameters){
				if(kvp.Key == "HomePhone"){
					_user.Properties["homePhone"].Value = kvp.Value;
				}
				else if(kvp.Key == "Page"){
					_user.Properties["pager"].Value = kvp.Value;
				}
				else if(kvp.Key == "Mobile"){
					_user.Properties["mobile"].Value = kvp.Value;
				}
				else if(kvp.Key == "Fax"){
					_user.Properties["facsimileTelephoneNumber"].Value = kvp.Value;
				}
				else if(kvp.Key == "IP"){
					_user.Properties["ipPhone"].Value = kvp.Value;
				}
				else if(kvp.Key == "Notes"){
					_user.Properties["info"].Value = kvp.Value;
				}
			}
			if(SaveChanges(nameof(UpdateTelephone))){
				return true;
			}
			return false;
		}

		//updates Profile tab
		public bool UpdateProfile(Dictionary<string, string> Parameters){
			if(UserExists(nameof(UpdateProfile)) == false){
				return false;
			};
			foreach(var kvp in Parameters){
				if(kvp.Key == "ProfilePath"){
					AddStackMessage("Updating ProfilePath to: " + kvp.Value);
					_user.Properties["profilePath"].Value = kvp.Value;
				}
				else if(kvp.Key == "LogonScript"){
					AddStackMessage("Updating LogonScript to: " + kvp.Value);
					_user.Properties["scriptPath"].Value = kvp.Value;
				}
				else if(kvp.Key == "HomeDirectory"){
					AddStackMessage("Updating HomeDirectory to: " + kvp.Value);
					_user.Properties["homeDirectory"].Value = kvp.Value;
				}
				else if(kvp.Key == "HomeDrive"){
					AddStackMessage("Updating HomeDrive to: " + kvp.Value);
					_user.Properties["homeDrive"].Value = kvp.Value;
				}
			}
			if(SaveChanges(nameof(UpdateProfile))){
				return true;
			}
			return false;
		}

		//Updates the Account tab
		public bool UpdateAccount(Dictionary<string, string> Parameters){
			if(UserExists(nameof(UpdateAccount)) == false){
				return false;
			};
			if(_user == new DirectoryEntry()){
				SetExceptionMessage(nameof(UpdateAccount), "User entry not set. Pleae run SetOU()");
				return false;
			}
			foreach(var kvp in Parameters){
				if(kvp.Key == "UPN"){
					_user.Properties["userPrincipalName"].Value = kvp.Value;
				}
				else if(kvp.Key == "AccountName"){
					_user.Properties["samAccountName"].Value = kvp.Value;
				}
			}
			if(SaveChanges(nameof(UpdateAccount))){
				return true;
			}
			return false;
		}

		//Updates the Address tab
		public bool UpdateAddress(Dictionary<string, string> Parameters){
			if(UserExists(nameof(UpdateAddress)) == false){
				return false;
			};
			foreach(var kvp in Parameters){
				if(kvp.Key == "Street"){
					_user.Properties["streetAddress"].Value = kvp.Value;
				}
				else if(kvp.Key == "POBox"){
					_user.Properties["postOfficeBox"].Value = kvp.Value;
				}
				else if(kvp.Key == "City"){
					_user.Properties["l"].Value = kvp.Value;
				}
				else if(kvp.Key == "State"){
					_user.Properties["st"].Value = kvp.Value;
				}
				else if(kvp.Key == "PostalCode"){
					_user.Properties["postalCode"].Value = kvp.Value;
				}
				else if(kvp.Key == "Country"){
					_user.Properties["msExchUsageLocation"].Value = kvp.Value;
					_user.Properties["c"].Value = kvp.Value;
				}
			}
			if(SaveChanges(nameof(UpdateAddress))){
				return true;
			}
			return false;
		}

		//Updates the General tab
		public bool UpdateGeneral(Dictionary<string, string> Parameters){
			if(UserExists(nameof(UpdateGeneral)) == false){
				return false;
			}
			foreach(var kvp in Parameters){
				if(kvp.Key == "FirstName"){
					_user.Properties["givenName"].Value = kvp.Value;
				}
				else if(kvp.Key == "LastName"){
					_user.Properties["sn"].Value = kvp.Value;
				}
				else if(kvp.Key == "DisplayName"){
					_user.Properties["displayName"].Value = kvp.Value;
				}
				else if(kvp.Key == "Initials"){
					_user.Properties["initials"].Value = kvp.Value;
				}
				else if(kvp.Key == "Description"){
					_user.Properties["description"].Value = kvp.Value;
				}
				else if(kvp.Key == "Office"){
					_user.Properties["physicalDeliveryOfficeName"].Value = kvp.Value;
				}
				else if(kvp.Key == "Email"){
					_upn = kvp.Value;
					_user.Properties["mail"].Value = kvp.Value;
				}
				else if(kvp.Key == "PhoneNumber"){
					_user.Properties["telephoneNumber"].Value = kvp.Value;
				}
				else if(kvp.Key == "Website"){
					_user.Properties["wWWHomePage"].Value = kvp.Value;
				}
			}
			if(SaveChanges(nameof(UpdateGeneral))){
				return true;
			}
			return false;
		}

		//determines if a path is valid or not
		public bool ValidOU(){
			AddStackMessage("Verifying that the path is valid: "+ _userOU.Path);
			var exists = _userOU.Guid.ToString();
			if(exists != null || exists != ""){
				AddStackMessage("Path is valid.");
				return true;
			}
			AddStackMessage("Path is not valid.");
			return false;
		}

		//creates an AD user inside of the specified OU
		public bool CreateADUser(string Name){
			AddStackMessage("Creating AD User: " + Name);
			if(ValidOU()){
				_user = _userOU.Children.Add("CN=" + Name, "user");
				_user.CommitChanges();
				SetPassword(CreateSecurePassword(60));
				AddStackMessage("AD User successfully created at:" + _user.Path);
				return true;
			}
			UserEntryException(nameof(CreateADUser));
			return false;
		}

		//use this if you want to be lazy and let the script handle everything(easiest way to use IMO)
		//Creates the user and passes all parameter values into the UpdateTab() methods
		public bool LazyCreateADUser(Dictionary<string, string> AllParameters, List<string> Groups){
			AddStackMessage("Beginning Lazy AD Creation");
			AddStackMessage("Checking that required parameters have been set");
			bool upnFound = false, usernameFound = false, fullNameFound = false;
			string fullName = "";
			foreach(var kvp in AllParameters){
				if(kvp.Key == "FullName" && kvp.Value != ""){
					fullName = kvp.Value;
					AddStackMessage("First Name found");
					fullNameFound = true;
				}
				else if(kvp.Key == "UPN" && kvp.Value != ""){
					AddStackMessage("UPN found");
					upnFound = true;
					_upn = kvp.Value;
				}
				else if(kvp.Key == "Username" && kvp.Value != ""){
					AddStackMessage("Username found");
					usernameFound = true;
					_username = kvp.Value;
				}
			}
			if(upnFound && usernameFound && fullNameFound && CreateADUser(fullName)){
				bool account = UpdateAccount(AllParameters);
				if(!account){
					AddStackMessage(nameof(UpdateAccount) + " Failed to update Account Tab.");
				}
				bool profile = UpdateProfile(AllParameters);
				if(!profile){
					AddStackMessage(nameof(UpdateProfile) + " Failed to update Profile Tab.");
				}
				bool general = UpdateGeneral(AllParameters);
				if(!general){
					AddStackMessage(nameof(UpdateGeneral) + " Failed to update General Tab.");
				}
				bool address = UpdateAddress(AllParameters);
				if(!address){
					AddStackMessage(nameof(UpdateAddress) + " Failed to update Address Tab.");
				}
				bool org = UpdateOrganization(AllParameters);
				if(!org){
					AddStackMessage(nameof(UpdateAddress) + " Failed to update Organization Tab.");
				}
				bool groups = AssignGroupsToUser(Groups);
				if(!groups){
					AddStackMessage(nameof(AssignGroupsToUser) + " Failed to add user to group(s)");
				}
				if(account && general && address && profile && org && groups){
					AddStackMessage("SUCESSFULLY CREATED USER: " + fullName + "-" + _upn);
					return true;
				}
				SetExceptionMessage(nameof(LazyCreateADUser), "Failed to update values for user");
				return false;
			}
			RequiredParametersException(nameof(LazyCreateADUser));
			return false;
		}
	}
}
