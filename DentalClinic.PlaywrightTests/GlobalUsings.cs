global using NUnit.Framework;
global using Microsoft.Playwright;
global using Microsoft.Playwright.NUnit;

// Force sequential test execution across all fixtures — E2E tests share one
// real server and database so parallel fixture runs cause login interference.
[assembly: Parallelizable(ParallelScope.None)]
