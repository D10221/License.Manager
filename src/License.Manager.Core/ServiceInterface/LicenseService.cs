//
// Copyright © 2012 - 2013 Nauck IT KG     http://www.nauck-it.de
//
// Author:
//  Daniel Nauck        <d.nauck(at)nauck-it.de>
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Web.Configuration;
using License.Manager.Core.Model;
using License.Manager.Core.ServiceModel;
using ServiceStack.CacheAccess;
using ServiceStack.Common;
using ServiceStack.Common.Web;
using ServiceStack.OrmLite;
using ServiceStack.ServiceInterface;

namespace License.Manager.Core.ServiceInterface
{
    [Authenticate]
    public class LicenseService : Service
    {
        private readonly IDbConnectionFactory _db;
        private readonly ICacheClient _cacheClient;

        public LicenseService(IDbConnectionFactory db, ICacheClient cacheClient)
        {
            _db = db;
            _cacheClient = cacheClient;
        }

        public object Get(GetLicenseTypes request)
        {
            return Enum.GetValues(typeof(Portable.Licensing.LicenseType))
                       .Cast<Portable.Licensing.LicenseType>().ToList();
        }

        public object Post(IssueLicense issueRequest)
        {
            MachineKeySection machineKeySection;
            Model.License license;
            Customer customer; 
            Product product; 
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                machineKeySection = WebConfigurationManager.GetSection("system.web/machineKey") as MachineKeySection;
                if (machineKeySection == null || StringComparer.OrdinalIgnoreCase.Compare(machineKeySection.Decryption, "Auto") == 0)
                    throw new Exception(Properties.Resources.InvalidMachineKeySection);              

                license = db.GetById<Model.License>(issueRequest.Id);
                if (license == null)
                    HttpError.NotFound("License not found!");

                Debug.Assert(license != null, "license != null");

                customer = db.GetById<Customer>(license.CustomerId);
                product = db.GetById<Product>(license.ProductId);
            }

            var licenseFile =
                Portable.Licensing.License.New()
                        .WithUniqueIdentifier(license.LicenseId)
                        .As(license.LicenseType)
                        .WithMaximumUtilization(license.Quantity)
                        .ExpiresAt(license.Expiration)
                        .LicensedTo(c =>
                                        {
                                            c.Name = customer.Name;
                                            c.Email = customer.Email;
                                            c.Company = customer.Company;
                                        })
                        .WithProductFeatures(license.ProductFeatures)
                        .WithAdditionalAttributes(license.AdditionalAttributes)
                        .CreateAndSignWithPrivateKey(product.KeyPair.EncryptedPrivateKey,
                                                     machineKeySection.DecryptionKey);

            var issueToken = Guid.NewGuid();

            _cacheClient.Set(UrnId.Create<Model.License>("IssueToken", issueToken.ToString()), licenseFile, new TimeSpan(0, 5, 0));

            return new HttpResult(new IssueLicenseResponse {Token = issueToken})
                       {
                           StatusCode = HttpStatusCode.Created,
                           Headers =
                               {
                                   {HttpHeaders.Location, Request.AbsoluteUri.AddQueryParam("token", issueToken)}
                               }
                       };
        }

        public object Get(DownloadLicense downloadRequest)
        {
            var cacheKey = UrnId.Create<Model.License>("IssueToken", downloadRequest.Token.ToString());
            var license = _cacheClient.Get<Portable.Licensing.License>(cacheKey);

            if (license == null)
                return new HttpResult(HttpStatusCode.NotFound);

            var responseStream = new MemoryStream();
            license.Save(responseStream);

            var response = new HttpResult(responseStream, "application/xml");
            response.Headers[HttpHeaders.ContentDisposition] = "attachment; filename=License.lic";

            return response;
        }

        public object Post(CreateLicense request)
        {
            Model.License license;
            Customer customer;
            Product product; 
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                license = new Model.License().PopulateWith(request);                
                customer = db.GetById<Customer>(license.CustomerId);                                           
                product = db.GetById<Product>(license.ProductId);
            }
            return
                new HttpResult(new LicenseDto
                                   {
                                       Customer = customer,
                                       Product = new ProductDto().PopulateWith(product)
                                   }.PopulateWith(license))
                    {
                        StatusCode = HttpStatusCode.Created,
                        Headers =
                            {
                                {HttpHeaders.Location, Request.AbsoluteUri.CombineWith(license.Id)}
                            }
                    };
        }

        public object Put(UpdateLicense request)
        {
            Model.License license;
            Customer customer;
            Product product;
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                license = db.GetById<Model.License>(request.Id);

                if (license == null)
                    HttpError.NotFound("License not found!");

                license.PopulateWith(request);
                
                db.Update(license);

                Debug.Assert(license != null, "license != null");

                customer = db.GetById<Customer>(license.Id);

                product = db.GetById<Product>(license.ProductId);
            }

            return new LicenseDto
                       {
                           Customer = customer,
                           Product = new ProductDto().PopulateWith(product)
                       }.PopulateWith(license);
        }

        public object Delete(UpdateLicense request)
        {
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                var license = db.GetById<Model.License>(request.Id);
                if (license == null)
                    HttpError.NotFound("License not found!");
                
                db.Delete(license);
            }

            return
                new HttpResult
                    {
                        StatusCode = HttpStatusCode.NoContent,
                    };
        }

        public object Get(GetLicense request)
        {
            Model.License license;
            Customer customer;
            Product product; 
            using (var db = _db.CreateDbConnection())
            {
                db.Open();               
                license = db.GetById<Model.License>(request.Id);
                if (license == null)
                    HttpError.NotFound("License not found!");

                Debug.Assert(license != null, "license != null");

                customer = db.GetById<Customer>(license.Id);
                product = db.GetById<Product>(license.ProductId);
            }
            return new LicenseDto
                       {
                           Customer = customer,
                           Product = new ProductDto().PopulateWith(product)
                       }.PopulateWith(license);
        }

        public object Get(FindLicenses request)
        {
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
               

                if (request.CustomerId.HasValue)
                {                    
                    return db.Select<Model.License>(x=>x.CustomerId == request.CustomerId.Value);
                }

                if (request.ProductId.HasValue)
                {                   
                    return db.Select<Model.License>(x => x.CustomerId == request.ProductId.Value);
                }

                
                return ToDto(db, db.Select<Model.License>());
            }
           
        }

        IEnumerable<LicenseDto> ToDto(IDbConnection cnx, IEnumerable<Model.License> licenses)
        {
            return licenses.Select(
                license =>
                {
                    var customer = cnx.GetById<Customer>(license.CustomerId);
                    var product = cnx.GetById<Product>(license.ProductId);
                    return new LicenseDto
                    {
                        Customer = customer,
                        Product =
                            new ProductDto().PopulateWith(product)
                    }.PopulateWith(license);
                });
        }
    }
}