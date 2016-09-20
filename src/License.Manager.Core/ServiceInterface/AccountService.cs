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
using System.Globalization;
using System.Net;
using License.Manager.Core.ServiceModel;
using ServiceStack.Common;
using ServiceStack.Common.Web;
using ServiceStack.OrmLite;
using ServiceStack.ServiceInterface;
using ServiceStack.ServiceInterface.Auth;

namespace License.Manager.Core.ServiceInterface
{
    [Authenticate]
    public class AccountService : Service
    {
        private readonly IDbConnectionFactory _db;
        private readonly IUserAuthRepository _userAuthRepository;

        public AccountService(IDbConnectionFactory db, IUserAuthRepository userAuthRepository)
        {
            this._db = db;
            _userAuthRepository = userAuthRepository;
        }

        public object Post(CreateAccount request)
        {
            var newUserAuth = request.TranslateTo<UserAuth>();
            newUserAuth.PrimaryEmail = request.Email;

            var newUser = _userAuthRepository.CreateUserAuth(newUserAuth, request.Password);

            return
                new HttpResult(newUser)
                {
                    StatusCode = HttpStatusCode.Created,
                    Headers =
                    {
                        {HttpHeaders.Location, Request.AbsoluteUri.CombineWith(newUser.Id)}
                    }
                };
        }

        public object Put(UpdateAccount request)
        {
            var newUserAuth = request.TranslateTo<UserAuth>();

            newUserAuth.PrimaryEmail = request.Email;

            var existingUser = _userAuthRepository.GetUserAuth(SessionAs<UserSession>(), null);

            var newUser = _userAuthRepository.UpdateUserAuth(existingUser, newUserAuth, request.Password);

            return newUser;
        }

        public object Delete(UpdateAccount request)
        {
            var accountId = string.Concat("UserAuths/", request.Id.ToString(CultureInfo.InvariantCulture));
            var account = _userAuthRepository.GetUserAuth(accountId);
            if (account == null)
            {
                throw HttpError.NotFound("Account not found!");
            }            
            return 
                new HttpResult
                {
                    StatusCode = HttpStatusCode.NoContent,
                };
        }

        public object Get(GetAccount request)
        {
            string id;
            if (request.Id?.ToString(CultureInfo.InvariantCulture) != null)
            {
                id = request.Id.Value.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                var session = SessionAs<UserSession>();
                id = session.UserAuthId;
            }
            
            var account = _userAuthRepository.GetUserAuthByUserName(id);
            if (account == null)
                throw HttpError.NotFound("Account not found!");

            var accountDto =
                new AccountDto
                {
                    User = account,
                    OAuthProviders =
                        _userAuthRepository.GetUserOAuthProviders(account.Id.ToString(CultureInfo.InvariantCulture))
                };

            return accountDto;
        }

        public object Get(FindAccounts request)
        {
            //var query = documentSession.Query<CustomerAllPropertiesIndex.Result, CustomerAllPropertiesIndex>();

            //if (!string.IsNullOrWhiteSpace(request.Name))
            //    query = query.Search(c => c.Query, request.Name);

            //if (!string.IsNullOrWhiteSpace(request.Email))
            //    query = query.Search(c => c.Query, request.Email);

            //var query = documentSession.Query<UserAuth>();

            //return query.OfType<UserAuth>().ToList();

           throw new NotImplementedException();
        }
    }
}