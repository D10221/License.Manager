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

using System.Net;
using License.Manager.Core.Model;
using License.Manager.Core.ServiceModel;
using ServiceStack.Common;
using ServiceStack.Common.Web;
using ServiceStack.OrmLite;
using ServiceStack.ServiceInterface;

namespace License.Manager.Core.ServiceInterface
{
    [Authenticate]
    public class CustomerService : Service
    {
        private readonly IDbConnectionFactory _db;

        public CustomerService(IDbConnectionFactory db)
        {
            _db = db;
        }

        public object Post(CreateCustomer request)
        {
            var customer = new Customer().PopulateWith(request);
            
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                db.Insert(customer);
            }

            return
                new HttpResult(customer)
                    {
                        StatusCode = HttpStatusCode.Created,
                        Headers =
                            {
                                {HttpHeaders.Location, Request.AbsoluteUri.CombineWith(customer.Id)}
                            }
                    };
        }

        public object Put(UpdateCustomer request)
        {
            Customer customer;//documentSession.Load<Customer>(request.Id);
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                customer = db.GetById<Customer>(request.Id);
                if (customer == null)
                    HttpError.NotFound("Customer not found!");

                customer.PopulateWith(request);
                
                db.Update(customer);
            }

            return customer;
        }

        public object Delete(UpdateCustomer request)
        {
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                var customer = db.GetById<Customer>(request.Id);
                if (customer == null)
                    HttpError.NotFound("Customer not found!");
                
                db.Delete(customer);
            }

            return new HttpResult { StatusCode = HttpStatusCode.NoContent, };
        }

        public object Get(GetCustomer request)
        {
            Customer customer; 
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                customer = db.GetById<Customer>(request.Id);
            }
            if (customer == null)
                HttpError.NotFound("Customer not found!");

            return customer;
        }

        public object Get(FindCustomers request)
        {
            using (var db = _db.CreateDbConnection())
            {
                db.Open();
                
                if (!string.IsNullOrWhiteSpace(request.Name))
                {                
                    return db.Select<Customer>(x => x.Name == request.Name);
                }

                if (!string.IsNullOrWhiteSpace(request.Company))
                {                 
                    return db.Select<Customer>(x => x.Company == request.Company);
                }

                if (!string.IsNullOrWhiteSpace(request.Email))
                {                    
                    return db.Select<Customer>(x => x.Email == request.Email);
                }

                return db.Select<Customer>();
            }            
        }
    }
}