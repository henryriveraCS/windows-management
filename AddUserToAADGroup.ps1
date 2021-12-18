#pass the following parameters in order to add a user to a specifc AAD mail security group
param(
    [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
    [string] $LoginEmail,
    [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
    [string] $UserUPN,
    [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
    [string[]] $Groups
)

#Specify that this script is being remotely executed(can be updated if needed)
Set-ExecutionPolicy RemoteSigned
#connect & login to exhchange online
try{
    Connect-ExchangeOnline -UserPrincipalName $LoginEmail
    $getSessions = Get-PSSession | Select-Object -Property State, Name
    #variable to determine if the login was successful/failed
    $isConnected = (@($getsessions) -like '@{State=Opened; Name=ExchangeOnlineInternalSession*').Count -gt 0
    if($isConnected -eq "True"){
        foreach($GroupName in $Groups){
            Add-DistributionGroupMember -Identity $GroupName -Member $UserUPN
        }
        return $true
    }
    return $false
} catch { return $false } #handle the error here however you want
