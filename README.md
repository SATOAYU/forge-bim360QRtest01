# design.automation.revit-csharp-bim360

![Platforms](https://img.shields.io/badge/platform-Windows|MacOS-lightgray.svg)
![.NET](https://img.shields.io/badge/.NET%20Core-2.1-blue.svg)
[![License](http://img.shields.io/:license-MIT-blue.svg)](http://opensource.org/licenses/MIT)

[![oAuth2](https://img.shields.io/badge/oAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![Data-Management](https://img.shields.io/badge/Data%20Management-v1-green.svg)](http://developer.autodesk.com/)
[![Viewer](https://img.shields.io/badge/Viewer-v6-green.svg)](http://developer.autodesk.com/)
[![Design-Automation-Revit](https://img.shields.io/badge/Design%20Automation%20Revit-v3-green.svg)](http://developer.autodesk.com/)

![Intermediate](https://img.shields.io/badge/Level-Intermediate-blue.svg)

# Description

Show BIM 360 Hubs, Projects and Files, based on this tutorial. When open a Revit model, a customized **Property Panel** allow changing **Comments** and **Marks** attributes. A new <kbd>R</kbd> button appears on the toolbar. On click, a `Workitem` is posted to Design Automation. When ready, a new version is created.


## Thumbnail

![](paramUpdater/wwwroot/img/designautomation_v3_revit.gif) 

## Live version

ToDo

# Setup

## Prerequisites

1. **Forge Account**: Learn how to create a Forge Account, activate subscription and create an app at [this tutorial](http://learnforge.autodesk.io/#/account/). 
2. **Visual Studio**: Either Community (Windows) or Code (Windows, MacOS).
3. **.NET Core** basic knowledge with C#
4. **Revit API**: 

## Running locally

Clone this project or download it. It's recommended to install [GitHub desktop](https://desktop.github.com/). To clone it via command line, use the following (**Terminal** on MacOSX/Linux, **Git Shell** on Windows):

    git clone git@github.com:augustogoncalves/design.automation.revit-csharp-bim360.git

**Visual Studio** (Windows):

Right-click on the project, then go to **Debug**. Adjust the settings as shown below. 

![](paramUpdater/wwwroot/img/visual_studio_settings.png) 

**Visual Sutdio Code** (Windows, MacOS):

Open the folder, at the bottom-right, select **Yes** and **Restore**. This restores the packages (e.g. Autodesk.Forge) and creates the launch.json file. See *Tips & Tricks* for .NET Core on MacOS.

![](paramUpdater/wwwroot/img/visual_code_restore.png)

At the `.vscode\launch.json`, find the env vars and add your Forge Client ID, Secret and callback URL. Also define the `ASPNETCORE_URLS` variable. The end result should be as shown below:

```json
"env": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "ASPNETCORE_URLS" : "http://localhost:3000",
    "FORGE_CLIENT_ID": "your id here",
    "FORGE_CLIENT_SECRET": "your secret here",
    "FORGE_CALLBACK_URL": "http://localhost:3000/api/forge/callback/oauth",
},
```

## Deployment

To deploy this application to Heroku, the **Callback URL** for Forge must use your `.herokuapp.com` address. After clicking on the button below, at the Heroku Create New App page, set your Client ID, Secret and Callback URL for Forge.

[![Deploy](https://www.herokucdn.com/deploy/button.svg)](https://heroku.com/deploy)

### Know issues

Heroku will timeout after 30 seconds. This sample doesn't yet implement the `callback`, so it will not work as expected there.  

Watch [this video](https://www.youtube.com/watch?v=Oqa9O20Gj0c) on how deploy samples to Heroku.

# Further Reading

Documentation:

- [BIM 360 API](https://developer.autodesk.com/en/docs/bim360/v1/overview/) and [App Provisioning](https://forge.autodesk.com/blog/bim-360-docs-provisioning-forge-apps)
- [Data Management API](https://developer.autodesk.com/en/docs/data/v2/overview/)
- [Viewer](https://developer.autodesk.com/en/docs/viewer/v6)
- Design Automation (ToDo for v3)

### Tips & Tricks

This sample uses .NET Core and works fine on both Windows and MacOS, see [this tutorial for MacOS](https://github.com/augustogoncalves/dotnetcoreheroku).

### Troubleshooting

1. **Cannot see my BIM 360 projects**: Make sure to provision the Forge App Client ID within the BIM 360 Account, [learn more here](https://forge.autodesk.com/blog/bim-360-docs-provisioning-forge-apps). This requires the Account Admin permission.

2. **error setting certificate verify locations** error: may happen on Windows, use the following: `git config --global http.sslverify "false"`

## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.

## Written by

Augusto Goncalves [@augustomaia](https://twitter.com/augustomaia), [Forge Partner Development](http://forge.autodesk.com)