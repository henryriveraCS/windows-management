using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

//for Directory Services(LDAP/Active Directory) calls
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
//for cryptographically secure password creations
using System.Security.Cryptography;

//class to insert/verify data into the local active directory instance
public class LocalActiveDirectoryUser
{
	//generates a random cryptographically secure password 20-bytes long
	//then pads it with some basic Windows password requirements (adds numbers, symbols, etc)
	//Note that the string of the SHA-256 hash is returned
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
                tmpPW = "ABC" + tmpPW;
            }
            if(!tmpPW.Any(char.IsSymbol)){
                tmpPW = tmpPW + "!?@";
            }
            if(!tmpPW.Any(char.IsLower)){
                tmpPW = tmpPW + "c";
            }
            if(!tmpPW.Any(char.IsNumber)){
                tmpPW = tmpPW + "12345";
            }
            return tmpPW;
        }
    }

    //returns an authenticated entry directory if the domain was successfully connected to
    private DirectoryEntry CreateDirectoryEntry(string domainName, string username, string password)
    {
    	//CHANGE .com to whatever TLD you use
        DirectoryEntry ldapConnection = new DirectoryEntry("LDAP://" + domainName + ".com", username, password);
        ldapConnection.AuthenticationType = AuthenticationTypes.Secure;

        return ldapConnection;
    }

    //will add the local AD user into the specified domain(by name) groups
    public bool AssignLocalGroupsToUser(string domainName, string userUPN, List<string> groups){
        if(groups != new List<string>() && userUPN != null){
			//create the principal context of the domain
            using(PrincipalContext pc = new PrincipalContext(ContextType.Domain, domainName)){
                foreach(string groupName in groups){
                    GroupPrincipal group = GroupPrincipal.FindByIdentity(pc, groupName);
                    group.Members.Add(pc, IdentityType.UserPrincipalName, userUPN);
                    //DO NOT RUN .DELETE() EVER OR IT WILL ITERATIVELY DELETE GROUPS - YOUR WELCOME
                    group.Save();
                }
            }
            return true;
        }
        return false;
    }

    //WIP: formatting ATM so it's usable consistent across different domains/OU's
    public bool CreateADUser(string DomainName, string TLD,
						Dictionary<string, string> AdminLogin,
						Dictionary<string, string> UserData,
						List<string> Groups)
	{
		return false;
    }
}
