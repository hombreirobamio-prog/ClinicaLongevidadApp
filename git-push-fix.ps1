# Script to push test project fix branch
# Run in repository root (PowerShell)

$branch = "fix/tests-project"

Write-Host "Creating and switching to branch $branch"
git checkout -b $branch

Write-Host "Adding test project file"
git add ClinicaLongevidadApp.Tests/ClinicaLongevidadApp.Tests.csproj

Write-Host "Committing"
git commit -m "Fix test project: align TFM, add Moq and update xunit.runner.visualstudio to 2.8.0"

Write-Host "Pushing to origin"
git push -u origin $branch

Write-Host "Done. Run 'git status' and open a PR on your remote if desired."