﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.WebTesting;
using Microsoft.VisualStudio.TestTools.WebTesting.Rules;
using NuGetGallery.FunctionalTests.Helpers;
using NuGetGallery.FunctionTests.Helpers;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// Uploads a new test package using Gallery UI in read-only mode and checks if proper error is shown.
    /// </summary>
    public class UploadPackageFromUIInReadOnlyMode : WebTest
    {
        public UploadPackageFromUIInReadOnlyMode()
        {
            PreAuthenticate = true;
        }

        public override IEnumerator<WebTestRequest> GetRequestEnumerator()
        {
            //run this test only if read-only mode is set. This is to avoid false failures while doing Run all tests locally.
            if (!EnvironmentSettings.ReadOnlyMode.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            ExtractHiddenFields defaultExtractionRule = AssertAndValidationHelper.GetDefaultExtractHiddenFields();

            //Do initial login
            WebTestRequest logonGet = AssertAndValidationHelper.GetLogonGetRequest();
            yield return logonGet;

            WebTestRequest logonPost = AssertAndValidationHelper.GetLogonPostRequest(this);
            yield return logonPost;

            WebTestRequest uploadRequest = AssertAndValidationHelper.GetHttpRequestForUrl(UrlHelper.UploadPageUrl);
            yield return uploadRequest;

            if (LastResponse.ResponseUri.ToString().Contains("verify-upload"))
            {
                // if there is a upload in progress, try to submit that upload instead of creating a new package (since we are just going to verify that upload doesn't go through).
                //Extract the package Id of the pending upload.
                string response = LastResponse.BodyString;
                int referenceIndex = response.IndexOf("<h4>Package ID</h4>", StringComparison.Ordinal);
                int startIndex = response.IndexOf("<p>", StringComparison.Ordinal);
                int endIndex = response.IndexOf("</p>", startIndex, StringComparison.Ordinal);
                string packageId = response.Substring(startIndex + 3, endIndex - (startIndex + 3));
                this.AddCommentToResult(packageId);   //Adding the package ID to result for debugging.
                WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.VerifyUploadPageUrl, Constants.ReadOnlyModeError, 503);
                yield return verifyUploadPostRequest;
            }
            else
            {
                // The API key is part of the nuget.config file that is present under the solution dir.
                string packageId = DateTime.Now.Ticks.ToString();
                string packageFullPath = PackageCreationHelper.CreatePackage(packageId).Result;

                WebTestRequest uploadPostRequest = AssertAndValidationHelper.GetUploadPostRequestForPackage(this, packageFullPath);
                yield return uploadPostRequest;

                WebTestRequest verifyUploadRequest = new WebTestRequest(UrlHelper.VerifyUploadPageUrl);
                verifyUploadRequest.ExtractValues += defaultExtractionRule.Extract;
                yield return verifyUploadRequest;

                WebTestRequest verifyUploadPostRequest = AssertAndValidationHelper.GetVerifyPackagePostRequestForPackage(this, packageId, "1.0.0", UrlHelper.VerifyUploadPageUrl, Constants.ReadOnlyModeError, 503);
                yield return verifyUploadPostRequest;
            }
        }
    }
}
