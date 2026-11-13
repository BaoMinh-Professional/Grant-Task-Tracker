@echo off
setlocal
set "exitCode=0"
set "frontendStatus=NOT RUN"
set "backendStatus=NOT RUN"

where npm.cmd >nul 2>nul
if errorlevel 1 goto :npm_missing

pushd "%~dp0frontend"
if errorlevel 1 goto :frontend_directory_error
echo [1/3] Installing frontend dependencies...
call npm.cmd ci
set "frontendInstallExitCode=%ERRORLEVEL%"
if not "%frontendInstallExitCode%"=="0" goto :frontend_install_failed

echo [2/3] Running frontend tests...
call npm.cmd run test
set "frontendExitCode=%ERRORLEVEL%"
popd
if not "%frontendExitCode%"=="0" goto :frontend_tests_failed
set "frontendStatus=PASSED"
goto :run_backend

:npm_missing
echo npm.cmd was not found. Frontend tests will be skipped. Install Node.js and npm to run them. 1>&2
set "frontendStatus=SKIPPED (npm.cmd not found)"
set "exitCode=1"

:run_backend
where dotnet >nul 2>nul
if errorlevel 1 goto :dotnet_missing

pushd "%~dp0backend"
if errorlevel 1 goto :backend_directory_error
echo [3/3] Running backend tests...
dotnet test GrantTracker.slnx
set "backendExitCode=%ERRORLEVEL%"
popd
if not "%backendExitCode%"=="0" goto :backend_tests_failed
set "backendStatus=PASSED"
goto :summary

:frontend_directory_error
echo Could not enter the frontend directory. Frontend tests will be skipped. 1>&2
set "frontendStatus=SKIPPED (frontend directory unavailable)"
set "exitCode=1"
goto :run_backend

:frontend_install_failed
popd
echo Frontend dependency installation failed. Frontend tests will be skipped. 1>&2
set "frontendStatus=SKIPPED (npm ci failed)"
set "exitCode=1"
goto :run_backend

:frontend_tests_failed
set "frontendStatus=FAILED"
set "exitCode=1"
echo Frontend tests failed; continuing with backend tests. 1>&2
goto :run_backend

:dotnet_missing
echo dotnet was not found. Backend tests will be skipped. Install the .NET 10 SDK to run them. 1>&2
set "backendStatus=SKIPPED (dotnet not found)"
set "exitCode=1"
goto :summary

:backend_tests_failed
set "backendStatus=FAILED"
set "exitCode=%backendExitCode%"
echo Backend tests failed. 1>&2
goto :summary

:backend_directory_error
echo Could not enter the backend directory. Backend tests will be skipped. 1>&2
set "backendStatus=SKIPPED (backend directory unavailable)"
set "exitCode=1"

:summary
echo.
echo ================= Test summary =================
echo Frontend tests: %frontendStatus%
echo Backend tests:  %backendStatus%
if "%exitCode%"=="0" echo Result: ALL TESTS PASSED
if not "%exitCode%"=="0" echo Result: ONE OR MORE TEST STEPS FAILED OR WERE SKIPPED
echo Failed test names and details, if any, are shown in the test output above.
echo =================================================
echo.
echo Press any key to close this window...
pause >nul
exit /b %exitCode%
