@echo off
setlocal

set "CSC=%~dp0bin\roslyn\csc.exe"
set "BIN=%~dp0bin"
set "SRC=%~dp0"
set "PKG=%~dp0..\packages"

"%CSC%" /target:library /out:"%BIN%\Final.dll" /pdb:"%BIN%\Final.pdb" /debug+ /optimize- /define:DEBUG;TRACE /nowarn:1701,1702 ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Data.dll ^
  /reference:System.Data.DataSetExtensions.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Web.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:System.Web.Routing.dll ^
  /reference:System.Web.Abstractions.dll ^
  /reference:System.Web.DynamicData.dll ^
  /reference:System.Web.Entity.dll ^
  /reference:System.Web.ApplicationServices.dll ^
  /reference:System.Web.Services.dll ^
  /reference:System.Xml.dll ^
  /reference:System.Xml.Linq.dll ^
  /reference:System.Configuration.dll ^
  /reference:System.ComponentModel.DataAnnotations.dll ^
  /reference:System.Net.Http.dll ^
  /reference:System.Net.Http.WebRequest.dll ^
  /reference:System.EnterpriseServices.dll ^
  /reference:System.Runtime.Serialization.dll ^
  /reference:Microsoft.CSharp.dll ^
  /reference:"%BIN%\System.Web.Mvc.dll" ^
  /reference:"%BIN%\System.Web.Helpers.dll" ^
  /reference:"%BIN%\System.Web.WebPages.dll" ^
  /reference:"%BIN%\System.Web.WebPages.Razor.dll" ^
  /reference:"%BIN%\System.Web.WebPages.Deployment.dll" ^
  /reference:"%BIN%\System.Web.Razor.dll" ^
  /reference:"%BIN%\System.Web.Optimization.dll" ^
  /reference:"%BIN%\Newtonsoft.Json.dll" ^
  /reference:"%BIN%\WebGrease.dll" ^
  /reference:"%BIN%\Antlr3.Runtime.dll" ^
  /reference:"%BIN%\Microsoft.Web.Infrastructure.dll" ^
  /reference:"%BIN%\Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll" ^
  "%SRC%\App_Start\BundleConfig.cs" ^
  "%SRC%\App_Start\FilterConfig.cs" ^
  "%SRC%\App_Start\RouteConfig.cs" ^
  "%SRC%\Controllers\AccountController.cs" ^
  "%SRC%\Controllers\AdminController.cs" ^
  "%SRC%\Controllers\DashboardController.cs" ^
  "%SRC%\Controllers\HomeController.cs" ^
  "%SRC%\Controllers\ProjectController.cs" ^
  "%SRC%\Controllers\UnitController.cs" ^
  "%SRC%\Controllers\BuildingController.cs" ^
  "%SRC%\Controllers\AccountsController.cs" ^
  "%SRC%\Controllers\ClientController.cs" ^
  "%SRC%\Controllers\MaterialAssignmentController.cs" ^
  "%SRC%\Data\DbHelper.cs" ^
  "%SRC%\Global.asax.cs" ^
  "%SRC%\Models\LoginViewModel.cs" ^
  "%SRC%\Models\ProductModel.cs" ^
  "%SRC%\Models\AccountsModels.cs" ^
  "%SRC%\Models\AdminModels.cs" ^
  "%SRC%\Models\ClientModels.cs" ^
  "%SRC%\Models\CustomizationReviewModel.cs" ^
  "%SRC%\Models\ProjectModel.cs" ^
  "%SRC%\Models\UserModel.cs" ^
  "%SRC%\Properties\AssemblyInfo.cs" ^
  "%SRC%\Security\PasswordHasher.cs" ^
  "%SRC%\Services\GeminiAdvisorService.cs"

echo Exit code: %ERRORLEVEL%
