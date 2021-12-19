using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Cryptography;

//WIP
//class to insert/verify data from the active directory instance
public class LocalActiveDirectoryUser
{
	//keeping track of internal variables to be used by different functions once this class is initialized
	private string _errorMsg = "";
	private string _domainName = "";
	private string _tld = "";
	private string _upn = "";
	private DirectoryEntry _baseEntry = null;
	private DirectoryEntry _userEntry = null;

	//if you are getting any error messages and are not sure why - use this
	public string GetErrorMessage(){
		return _errorMsg;
	}

	//generates a random cryptographically secure password of a random-length
	//then pads it with some basic Windows password requirements (adds numbers, symbols, etc)
	private string CreateSecurePassword()
	{
		RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

		byte[] randomLetters = new byte[20];
		rngCsp.GetBytes(randomLetters);

		rngCsp.Dispose();

		string tmp = BitConverter.ToString(randomLetters);
		using( var hash = SHA256.Create() ){
			var byteArray = hash.ComputeHash(Encoding.UTF8.GetBytes( tmp) );
			string tmpPW = Convert.ToHexString(byteArray) + "!";
			//check to make sure it has all server password complexity requirements(you may want to change this!)
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
	}

	//evaluate if a username is valid
	public bool UsernameIsValid(DirectoryEntry UserDir, string Username){
		return true;
	}

	public bool EmailIsValid(DirectoryEntry UserDir, string Email){
		return true;
	}

	//returns an authenticated entry directory if the domain was successfully connected to
	private DirectoryEntry CreateDirectoryEntry(string Username, string Password, string DomainName, string TLD)
	{
		try{
			DirectoryEntry ldapConnection = new DirectoryEntry("LDAP://" + DomainName + "." + TLD, Username, Password);
			ldapConnection.AuthenticationType = AuthenticationTypes.Secure;
			return ldapConnection;
		} catch(Exception e){
			_errorMsg = "Exception caught at CreateDirectoryEntry() \n" + e.ToString();
			return null;	
		}
		
	}

	//will add the AD user into the passed local AD groups
	public bool AssignLocalGroupsToUser(List<string> groups){
		if(groups != new List<string>()){
			try{
				//create the scope to find each group in the domain
				using(PrincipalContext pc = new PrincipalContext(ContextType.Domain, _domainName)){
					foreach(string groupName in groups){
						GroupPrincipal group = GroupPrincipal.FindByIdentity(pc, groupName);
						group.Members.Add(pc, IdentityType.UserPrincipalName, userUPN);
						//DO NOT RUN .DELETE() EVER OR IT WILL ITERATIVELY DELETE GROUPS - YOUR WELCOME
						group.Save();
					}
				}
				return true;
			}
			catch(Exception e){
				_errorMsg = "Exception caught at AssignLocalGroupToUser() \n" + e.ToString();
			}
		} else{
			_errorMsg = "Groups is empty. Please pass a List<string> of the groups you want added";
			return false;
		}
	}

	//This should always be executed first in order to properly execute other functions
	//verifies the admin credentials are valid by signing in and sets internal AD values
	public bool ConnectToAD(string Username, string Password, string DomainName, string TLD){
		_baseEntry = null;
		try{
			_baseEntry = CreateDirectoryEntry(Username, Password, DomainName, TLD);
			if(_baseEntry != null){
				_domainName = DomainName;
				_tld = TLD;
				return true;
			}
			_errorMsg = "Failed to connect to AD. Please verify your input parameters";
			return false;
		} catch(Exception e){
			_errorMsg = "Exception caught at ConnectToAD() \n" + e.ToString();
			return false;
		}
	}

	//E.G: if your OU for a user is Domain.COM/America/Washington/Department/Sales/Users
	//then UserOU should be a List<string> with values: ["America", "Washington", "Department", "Sales", "Users"]
	//point the internal userEntry to the specified User OU
	public bool ChangeOU(List<string> OUList){
		int totalOUCount = UserOU.Count;
		if(totalOUCount <= 0){
			_errorMsg = "Not enough arguments in OUList, please make sure you have at least 1 item in the list";
			return false;
		}
		try{
			//change to for(int OUCount=0;etc;etc++) later
			_userEntry = _baseEntry;
			int currentOUCount = 0;
			foreach(string OU in UserOU){
				_userEntry = _userEntry.Children.Add("OU=", UserOU[currentOUCount], "CN");
				currentOUCount += 1;
			}
			return true;
		} catch(Exception e){
			_errorMsg = "Exception caught at ChangeOU() \n" + e.ToString();
			return false;
		}
	}
	/*
	When you create the dictionary, pass along the values in the format of "Key Name": "KeyValue"
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
	//WIP - WORKING ON FIXES
	//creates a user inside of the local AD instance
	public bool CreateADUser( Dictionary<string, string> UserParameters)
	{
		//login-related
		string empUsername = "", empEmail = "";
		//address-related
		string officeStreet = "", officeCity = "", officeZip = "", officeState = "", officeCountry = "";
		//extract the data into usable pieces from the "Parameters" dictionary
		//general-related
		string empPrefName = "", empFirstName = "", empMiddleName = "", empLastName = "";
		//Company-related
		string empDepartment = "", empPosition = "", empCompanyName = "";
		//path-related
		string profilePath = "", homeDir = "", logonScript = "", homeDrive = "";

		foreach(var kvp in UserParameters){
			if(kvp.Key == "Username"){
				empUsername = kvp.Value;
			}
			else if(kvp.Key == "Email"){
				empEmail = kvp.Value;
			}
			else if(kvp.Key == "FirstName"){
				empFirstName = kvp.Value;
			}
			else if(kvp.Key == "PrefName"){
				empPrefName = kvp.Value;
			}
			else if(kvp.Key == "MiddleName"){
				empMiddleName = kvp.Value;
			}
			else if(kvp.Key == "LastName"){
				empLastName = kvp.Value;
			}
			else if(kvp.Key == "Position"){
				empPosition = kvp.Value;
			}
			else if(kvp.Key == "Department"){
				empDepartment = kvp.Value;
			}
			else if(kvp.Key == "CompanyName"){
				empCompanyName = kvp.Value;
			}
			else if(kvp.Key == "ProfilePath"){
				profilePath = kvp.Value;
			}
			else if(kvp.Key == "HomeDirectory"){
				homeDir = kvp.Value;
			}
			else if(kvp.Key == "LogonScript"){
				logonScript = kvp.Value;
			}
			else if(kvp.Key == "HomeDrive"){
				homeDrive = kvp.Value;
			}
			else if(kvp.Key == "OfficeStreet"){
				officeStreet = kvp.Value;
			}
			else if(kvp.Key == "OfficeCity"){
				officeCity = kvp.Value;
			}
			else if(kvp.Key == "OfficeZip"){
				officeZip = kvp.Value;
			}
			else if(kvp.Key == "OfficeState"){
				officeState = kvp.Value;
			}
			else if(kvp.Key == "OfficeCountry"){
				officeCountry = kvp.Value;
			}
		}

		string tmpPassword = CreateSecurePassword();
		//verify that the username doesn't already exist locally
		if(UsernameIsValid(userDir, empUsername)){
			//build out the full name of the employee(middle name included if applicable)
			string tmpFullName = "";
			if(empFirstName != ""){
				tmpFullName = empFirstName + " ";
			} else{ 
				_errorMsg = "Please provide a FirstName key value in order to create an AD User";
				return false 
			}
			if(empMiddleName != ""){
				tmpFullName = tmpFullName + empMiddleName + " ";
			}
			if(empLastName != ""){
				tmpFullName = tmpFullName + empLastName;
			} else{
				_errorMsg = "Please provide a LastName key value in order to create an AD User";
				return false;
			}
			//create the user OU
			DirectoryEntry newUser = _userEntry.Children.Add("CN=" + tmpFullName, "User");
			newUser.Properties["samAccountName"].Value = empUsername;
			try{
				newUser.CommitChanges();
			} catch(Exception e){
				_errorMsg = "Exception while creating new user \n" + e.ToString();
				return false;
			}
			//proceed to update all login values
			newUser.Invoke("SetPassword", tmpPassword);

			newUser.Properties["givenName"].Value = empFirstName;
			newUser.Properties["sn"].Value = empLastName;
			newUser.Properties["displayName"].Value = tmpFullName;
			newUser.Properties["physicalDeliveryOfficeName"].Value = officeState;
			newUser.Properties["mail"].Value = empEmail;
			newUser.Properties["userPrincipalName"].Value = empEmail;
			try{
				newUser.CommitChanges();
			} catch(Exception e){
				_errorMsg = "Exception caught while updating password and setting email for new user \n" + e.ToString();
				return false;
			}
			if( empDepartment != ""){
				newUser.Properties["department"].Value = empDepartment;
			}
			if( empUsername != ""){
				newUser.Properties["mailNickname"].Value = empUsername;
			}
			if(officeStreet != ""){
				newUser.Properties["streetAddress"].Value = officeStreet;
			}
			if(officeZip != ""){
				newUser.Properties["postalCode"].Value = officeZip;
			}
			if(officeCity != ""){
				newUser.Properties["l"].Value = officeCity;
			}
			newUser.CommitChanges();
			if(officeState != ""){
				newUser.Properties["st"].Value = officeState;
			}
			if(officeCountry != ""){
				//usage location for licensing(for AAD)
				newUser.Properties["msExchUsageLocation"].Value = officeCountry;
				//country for AD
				newUser.Properties["c"].Value = officeCountry;
			}
			if(profilePath != ""){
				newUser.Properties["profilePath"].Value = profilePath;
			}
			if(logonScript != ""){
				newUser.Properties["scriptPath"].Value = logonScript;
			}
			if(homeDir != "" && homeDrive != ""){
				newUser.Properties["homeDrive"].Value = homeDrive;
				newUser.Properties["homeDirectory"].Value = homeDir;
			}
			if(companyName != ""){
				newUser.Properties["company"].Value = companyName;
			}
			try{
				newUser.CommitChanges();
			} catch(Exception e){
				_errorMsg = "Error while committing new user changes after creation \n" + e.ToString();
				return false;
			}

			//close off the directory
			newUser.Close();
			return true;
		}
		return false;
	}
}
