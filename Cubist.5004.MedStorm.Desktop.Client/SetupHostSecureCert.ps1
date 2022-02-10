# If we need to create a new cert unmark the ones below otherwise use the cert located in project
# $certificate = Get-Item -Path "localhost.cer"
#     -Subject medstormnew `
#     -DnsName medstormnew `
#     -KeyAlgorithm RSA `
#     -KeyLength 2048 `
#     -NotBefore (Get-Date) `
#     -NotAfter (Get-Date).AddYears(20) `
#     -CertStoreLocation "cert:CurrentUser\My" `
#     -FriendlyName "medstorm host Certificate for .NET Core" `
#     -HashAlgorithm SHA256 `
#     -KeyUsage DigitalSignature, KeyEncipherment, DataEncipherment `
#     -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1")  `
    
 #If we need to create new cert unmark this one otherwise use the cert located in project    
 #$certificatePath = 'Cert:\CurrentUser\My\' + ($certificate.ThumbPrint)  

# set certificate password here
$pfxPassword = ConvertTo-SecureString -String "Medstorm5004PS" -Force -AsPlainText
$pfxFilePath = "localhost.pfx"
$cerFilePath = "localhost.cer"

# If we need to create new files unmark the Export rows otherwise use the certs locaded in project
#Export-PfxCertificate -Cert $certificatePath -FilePath $pfxFilePath -Password $pfxPassword
#Export-Certificate -Cert $certificatePath -FilePath $cerFilePath

# import the pfx certificate
 Import-PfxCertificate -FilePath $pfxFilePath Cert:\LocalMachine\My -Password $pfxPassword -Exportable

# trust the certificate by importing the pfx certificate into your trusted root
 Import-Certificate -FilePath $cerFilePath -CertStoreLocation Cert:\CurrentUser\Root

$rootcert = Get-ChildItem Cert:\LocalMachine\My\ | Where-Object{$_.Subject -eq "CN=medstorm"}
$newSignedCert = New-SelfSignedCertificate -certstorelocation cert:\localmachine\my -dnsname "medstorm_signed" -Signer $rootcert
$pwd2 = ConvertTo-SecureString -String "Medstorm5004PS" -Force -AsPlainText
$certificatePath = 'Cert:\LocalMachine\My\' + ($newSignedCert.ThumbPrint)
Export-PfxCertificate -cert $certificatePath  -FilePath medstorm_signed_pfx.pfx -Password $pwd2
Export-Certificate -Cert $certificatePath  -FilePath medstorm_signed_crt.crt

Import-PfxCertificate -FilePath ".\medstorm_signed_pfx.pfx" Cert:\LocalMachine\My -Password $pwd2 -Exportable
Import-Certificate -FilePath ".\medstorm_signed_crt.crt" -CertStoreLocation Cert:\CurrentUser\Root