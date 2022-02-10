# 1. Introduction

A desktop application that is suppose to run on a laptop.
The application listens to a bluetooth signal from the PainSensor.
A combined package of values is recieved and passed to the client via signalR.
The client parses the package into several values that are displayed in a webbrowser.

## 2. Getting Started

Clone the repo to your computer
git clone https://cubist@dev.azure.com/cubist/Cubist.5004.MedStorm/_git/CubistFrontEnd
open the solution in VS and build.

### 3. Prerequisites

- Visual Studio 2019
- Aspnet.Core v3.1.0
- .net.Core v3.1.0
- Wix Toolset v3.11.2
  https://wixtoolset.org/releases/
- Wix Toolset Visual Studio 2019 extension
  Download in visual studio 2019 - Tools - Manage extensions-Online 
- Download the Packages folder from https://pareegroup.sharepoint.com/teams/CubistMed-storm5004SkinConductance/Jaetut%20asiakirjat/General/PSS%20Application/Packages and put it at the root of the project

## 4. Installing

## 4.1 Mock data
- Change appsettings.json "Mock":  true.
- Build the solution.
- Run.

## 4.2 Bluetooth sensor
- Change appsettings.json "Mock":  false if necessary.
- Change appsettings.json "AdvertisingName": "PainSensor" if necessary.
- Build the solution.
- Run.

## 4.3 Make https://medstorm:5001 secure
If we are doing the deployment for the first time we need to import the certificates fort the browsers. There is two ways of doing this, see `4.3.1 and 4.3.2`

## 4.3.1 Run powershell script (Use for development otherwise see 5.2.2)
In bin folder `bin\(Debug or Release)\netcoreapp3.1`
- Open powershell (or  run windows terminal) as administrator
-  `./SetupHostSecureCert.ps`
- Accept the pop ups for the certificates
- It should be no errors.

If using Firefox browser these steps need to be done:
- Enter “about:config” in Firefox browser 
- When a warning comes up, click “Accept the Risk and Continue”
- Search for preference "security.enterprise_roots.enabled" and set it to true.
- Restart webbrowser

Add medstorm to host file 
- Open notepad.exe as administrator
- Locate `C:\Windows\System32\drivers\etc\hosts`
- Add row `127.0.0.1 medstorm`
- Save

## 4.3.2 Run the setup installer
See chapter 5.2.2

## 4.3.4
If you need new certificates there is rows in `./SetupHostSecureCert.ps`. They are commented in the script and need to be uncommented.
Then run the script as chapter `4.3.1`

## 5. Deployment

## 5.1 Dependencies:
.net Core v3.x sdk

## 5.2 Deploy installation:

## 5.2.1 Debug mode
__Create a publish local folder__

- Right click the "Client" project and select "Properties". Change the "Output type" to "Console Application"
- Right-Click on the "Cubist.5004.MedStorm.Desktop.Client" project and choose Publish.
- Choose local folder and choose a suitable location.
- Press Publish.
- Copy this folder to the deploy computer.

__or create a MSI setup file__

- Edit version to release version in SetupPSSDesktopWix/Product.wxs 
- Right click the "Client" project and select "Properties". Change the "Output type" to "Console Application"
- Rebuild solution
- Find the project setup msi in SetupPSSDesktopWix/bin
- Run the setup file

If using Firefox browser these steps need to be done:
- Enter “about:config” in Firefox browser 
- When a warning comes up, click “Accept the Risk and Continue”
- Search for preference "security.enterprise_roots.enabled" and set it to true.
- Restart webbrowser

Application shortcut can be found in start menu - PSS application


## 5.2.2 Production mode

- Edit version to release version in SetupPSSDesktopWix/Product.wxs 
- Set `SpecToolPath/UseAbsolutePath` to `false` in `appsettings.json`
- Right click the "Client" project and select "Properties". Change the "Output type" to "Windows Application"
- Rebuild solution
- Find the project setup msi in SetupPSSDesktopWix/bin
- Run the setup file

If using Firefox browser these steps need to be done:
- Enter “about:config” in Firefox browser 
- When a warning comes up, click “Accept the Risk and Continue”
- Search for preference "security.enterprise_roots.enabled" and set it to true.
- Restart webbrowser

Application shortcut can be found in start menu - PSS application

## 6 Run the application from Deploy folder

## 6.1 Run with mock data

- Locate appSettings.json. 
- Change "Mock":  true.
- Save.
- Double-click Start.bat.
- If success, the internet host should run the application.

## 6.2 Run with Bluetooth painsensor

- Locate the Deploy folder.
- Locate appSettings.json. 
- Make sure "Mock":  false.
- Make sure "AdvertisingName": "PainSensor".
- Double-click Start.bat.
- If success, the internet host should run the application.

## 7 Using philips monitor

## 7.1 Run Philips monitor module:

At the moment, We need to copy these files manually to work during development
 - Copy SpecTool folder from project directory to C:\medstorm
 
## 7.2 Update philips monitor files
If the file location length is too long (for ex if the project is saved in doc/cubist/cubist...). Copy SpecTool folder for ex C:\medstorm\
- Update Medstorm.txt with preferred values
- in command window write 
```
	.\SpecTool.exe -r .\MedStorm.rpt -t .\MedStorm.bin .\MedStorm.txt
```
- Check no error messages has been reported.
- Copy the medstorm.txt, medstorm.rpt and medstorm.bin to CubistFrontEnd\Cubist.5004.MedStorm.Desktop.Core\PatientMonitor\SpecTool
- if added or removed data from the medstorm.txt, probably the OperatingSpecResponse need to be updated.
- Try to run the software
- For more information check the Philips monitor documentation
	
## 7.3 Connect from PSS app to Philips monitor:
- Connect the Philips patient monitor to the tablet or laptop using an Intellibridge EC5 to VGA to USB. The USB end is plugged in directly to the USB port of the tablet.
- Turn the patient monitor on and wait for it to start.
- Launch the application on the tablet and connect to the sensor.
- Wait until the sensor is connected and you can see the graphs.
- Click on the "Connect to monitor" button in the application.
- After a couple of seconds, the indices should be displayed on the patient monitor.


## 7.4 Troubleshoot problems with connection from PSS app to Philips monitor
- Check if your device recognizes the connector you are using, by opening Control Panel -> Device Manager
- Check if drivers on your laptop/tablet need to be updated, and laptop/tablet may need restart


## 8 Creating an .msi file to ship to customer
When creating an .msi installer:
- Set `SpecToolPath/UseAbsolutePath` to `false` in `appsettings.json`
- Increase version in SetupPSSDesktopWix/Product.wxs so that it is the same as in Release notes

## 9 Versioning

We use Git for versioning.
