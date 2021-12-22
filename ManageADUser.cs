using System;
using System.Linq; //Enumerable.Any
//using System.Text;
using System.Collections.Generic;
//Used to check that the code is running under Windows OS
using System.Runtime.Versioning;
//AD calls such as DirectoryEntry
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
//for password generation
using System.Security.Cryptography;

//class to insert/verify data from the active directory instance
namespace SampleNamespace
{
	//class to manage AD users
	[SupportedOSPlatformAttribute("windows")]
	public class ActiveDirectoryUser
	{
		//keeping track of internal variables to be used by different functions once this class is initialized
		private string _exceptionMsg = "";
		private string _stackMsg = "";
		private string _domainName = "";
		private string _tld = "";
		private string _fullName = "";
		private string _username = "";
		private string _upn = "";
		//OU's pointing to base of AD + user 
		private DirectoryEntry _baseOU = new DirectoryEntry();
		private DirectoryEntry _userOU = new DirectoryEntry();
		//Entry representing the user instance
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
				DirectoryEntry ldapConnection = new DirectoryEntry(fullPath, Username, Password);
				ldapConnection.AuthenticationType = AuthenticationTypes.Secure;
				AddStackMessage("Successfully connected to:" + fullPath);
				return ldapConnection;
			} catch(Exception e){
				SetExceptionMessage(nameof(CreateDirectoryEntry), e.Message);
				return new DirectoryEntry();	
			}
			
		}

		//will add the AD user into the passed local AD groups
		public bool AssignLocalGroupsToUser(List<string> Groups){
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
					SetExceptionMessage(nameof(AssignLocalGroupsToUser), e.Message);
					return false;
				}
			} else{
				SetExceptionMessage(nameof(AssignLocalGroupsToUser), "Groups is empty. Please pass a List<string> of the groups you want added");
				return false;
			}
		}

		//This should always be executed first in order to properly execute other functions
		//verifies the admin credentials are valid by signing in and sets internal AD values
		public bool Connect(string Username, string Password, string DomainName, string TLD){
			AddStackMessage(nameof(Connect) + " has been initialized. Resetting _baseOU");
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

		//E.G: if you want to create a user in OU Domain.COM/America/Washington/Department/Sales/Users
		//then OUList should be a List<string> with values: ["America", "Washington", "Department", "Sales", "Users"]
		//points the internal _userEntry to the specified User OU
		public bool ChangeOU(List<string> OUList){
			int totalOUCount = OUList.Count;
			if(totalOUCount <= 0){
				SetExceptionMessage(nameof(ChangeOU), "Not enough arguments in OUList, please make sure you have at least 1 item in the list");
				return false;
			}
			try{
				_userOU = _baseOU;
				for(int current = 0; current < totalOUCount; current++){
					_userOU = _userOU.Children.Add("OU=" + OUList[current], "CN");
				}
				return true;
			} catch(Exception e){
				SetExceptionMessage(nameof(ChangeOU), e.Message);
				return false;
			}
		}

		/*
		When you create the dictionary, pass along the each key pair value in the format of "Key Name": "KeyValue"
		REQUIRED PARAMETERS:
			"Username",
			"Email",
			"FirstName",
			"LastName"
		Account: 
			"Username": "Username of the AD Object"
			"Email": "Email address of the AD Object"
		General:
			"FirstName": "First name of AD Object"
			"PrefName": "Nickname of AD Object"
			"MiddleName": "Middle name of AD Object"
			"Last Name": "Last Name of AD Object"
		Address:
			"Street": "Street address here"
			"POBox": "PO Box address here"
			"State": "State/Province here"
			"ZipCode": "zip/postal code here"
			"Country": "2 letter country code - see ISO 3166 https://www.iso.org/obp/ui/#search"
		Profile:
			"LogonScript": "logon script name here"
			"HomeDrive": "Letter to map home drive to here"
			"HomeDirectory": "full path to directory server here"
			Organization:
			"JobTitle": "Job title here"
			"Department": "Department title here"
			"CompanyName": "Company Name here"
		Member Of:
				*See AssignLocalGroupsToUser for more information*
		*/

		//used to check that a user instance exists. Returns true if yes,
		//sets an exception method and returns false if no
		private bool UserExists(string MethodName){
			if(_user != new DirectoryEntry() && _user != _userOU){
				AddStackMessage("User Instance exists under: " + MethodName);
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
					_user.Properties["co"].Value = kvp.Value;
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
				else if(kvp.Key == "ScriptPath"){
					_user.Properties["scriptPath"].Value = kvp.Value;
				}
				else if(kvp.Key == "HomeDirectory"){
					_user.Properties["homeDirectory"].Value = kvp.Value;
				}
				else if(kvp.Key == "HomeDrive"){
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
					//sets Exchange location for license assignment
					_user.Properties["msExchUsageLocation"].Value = kvp.Value;
					//sets the AD country
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
					_fullName = kvp.Value;
					_user.Properties["givenName"].Value = kvp.Value;
				}
				else if(kvp.Key == "LastName"){
					_fullName += " " + kvp.Value;
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
		public bool ValidOU(string Path){
			AddStackMessage("Verifying that the path is valid: "+ Path);
			if(DirectoryEntry.Exists(Path)){
				AddStackMessage("Path is valid.");
				return true;
			}
			AddStackMessage("Path is not valid.");
			return false;
		}

		//creates an AD user inside of the specified OU
		//if this returns false/exception than either the name is not valid or OU is not valid
		public bool CreateADUser(string Name){
			AddStackMessage("Creating AD User: " + Name);
			if(ValidOU(_userOU.Path)){
				_user = _userOU.Children.Add("CN=" + _fullName, "user");
				_user.CommitChanges();
				SetPassword(CreateSecurePassword(60));
				AddStackMessage("AD User successfully created:" + _user.Path);
				return true;
			}
			UserEntryException(nameof(CreateADUser));
			return false;
		}

		//use this if you want to be lazy and let the script handle everything(easiest way to use IMO)
		//Creates the user and passes all parameter values into the UpdateTab() methods
		public bool LazyCreateADUser(Dictionary<string, string> AllParameters){
			AddStackMessage("Beginning Lazy AD Creation");
			AddStackMessage("Checking that required parameters have been set");
			foreach(var kvp in AllParameters){
				if(kvp.Key == "FirstName"){
					AddStackMessage("First Name found");
					_fullName = kvp.Value;
				}
				else if(kvp.Key == "LastName"){
					AddStackMessage("Last Name found");
					_fullName += " " + kvp.Value; 
				}
				else if(kvp.Key == "UPN"){
					AddStackMessage("UPN found");
					_upn = kvp.Value;
				}
				else if(kvp.Key == "Username"){
					AddStackMessage("Username found");
					_username = kvp.Value;
				}
			}
			AddStackMessage("Validating Full Name: " + _fullName);
			if(CreateADUser(_fullName)){
				bool account = UpdateAccount(AllParameters);
				bool password = SetPassword(CreateSecurePassword(60));
				bool general = UpdateGeneral(AllParameters);
				bool address = UpdateAddress(AllParameters);
				bool profile = UpdateProfile(AllParameters);
				bool org = UpdateOrganization(AllParameters);
				if(account && password && general && address && profile && org){
					return true;
				}
				return false;
			}
			RequiredParametersException(nameof(LazyCreateADUser));
			return false;
		}
	}
}
