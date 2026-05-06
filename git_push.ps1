# Git Push Automation Script for NixPackTrace
# This script adds all changes, commits them, and pushes to the 'main' branch.

$commitMessage = $args[0]
if (-not $commitMessage) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $commitMessage = "Auto-update: $timestamp"
}

Write-Host "--- Starting Git Push Process ---" -ForegroundColor Cyan

Write-Host "Adding changes..."
git add .

Write-Host "Committing changes with message: '$commitMessage'..."
git commit -m "$commitMessage"

Write-Host "Pushing to origin main..."
git push origin main

Write-Host "--- Git Push Completed Successfully! ---" -ForegroundColor Green
