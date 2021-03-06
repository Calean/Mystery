﻿using Mystery.Configuration;
using Mystery.Content;
using Mystery.MysteryAction;
using Mystery.Register;
using Mystery.Users;
using Mystery.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Mystery.Authentication
{

    public class SocialUser
    {
        public string provider { get; set; }
        public string id { get; set; }
        public string email { get; set; }
        public string name { get; set; }
        public string image { get; set; }
        public string token { get; set; }
        public string idToken { get; set; }

    }


    public class GoogleToken{
        public string sub { get; set; }
        public string exp { get; set; }
    }
    public class FacebookToken
    {
        public string name { get; set; }
        public string id { get; set; }
    }


    [PublishedAction(input_type: typeof(SocialUser), output_type: typeof(User), url = nameof(SocialLogin))]
    public class SocialLogin : BaseMysteryAction<SocialUser, User>, ICanRunWithOutLogin
    {

        private string validateGoogleToken() {
            var url = "https://oauth2.googleapis.com/tokeninfo?id_token=" + input.idToken;
            var c = new WebClient();
            try
            {
                var json = c.DownloadString(url);
                var from_google = JsonConvert.DeserializeObject<GoogleToken>(json);
                if (string.IsNullOrWhiteSpace(from_google.exp))
                    return null;
                var unix_time = Int32.Parse(from_google.exp);
                var expiration_date = new DateTime(1970, 1, 1).AddSeconds(unix_time);
                if (expiration_date > DateTime.Now.ToUniversalTime()) {
                    return from_google.sub;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }


        private ActionResult<User> retriveGoogleAccount(string id) {
            //live id cookies
            var cd = this.getGlobalObject<IContentDispatcher>();
            User user = cd.GetAllByFilter<UserSocialLogin>(
                x => x.provider_name == "google" && x.user_unique_id == id).FirstOrDefault()?.user;

            var googleIDconf = this.getGlobalObject<IConfigurationProvider>().getConfiguration<GoogleIDConfiguration>();
            var session = this.getGlobalObject<MysterySession>();

            //first time in this instance
            if (user == null)
            {
                if (!googleIDconf.allow_new_users)
                    return ActionResultTemplates<User>.UnAuthorized;

                var cc = this.getGlobalObject<IGlobalContentCreator>();
                if (session.authenticated_user == null)
                {
                    user = cc.getAndAddNewContent<User>();
                    user.username = "google:" + id;
                    user.fullname = "google:" + id;
                }
                else
                {
                    user = session.authenticated_user;
                }
                session.authenticated_user = user;

                var user_social_login = cc.getAndAddNewContent<UserSocialLogin>();
                user_social_login.user = user;
                user_social_login.user_unique_id = id;
                user_social_login.provider_name = "google";
            }

            session.authenticated_user = user;

            return user;
        }

        protected override ActionResult<User> ActionImplemetation()
        {
            if (input.provider == "google") {
                var googleIDconf = this.getGlobalObject<IConfigurationProvider>().getConfiguration<GoogleIDConfiguration>();
                if (!googleIDconf.enabled)
                    return ActionResultTemplates<User>.UnAuthorized;

                var google_id = validateGoogleToken();
                if(string.IsNullOrWhiteSpace(google_id))
                    return ActionResultTemplates<User>.InvalidInput;
                return this.retriveGoogleAccount(google_id);
            }
            if (input.provider == "facebook") {
                var facebookIDconf = this.getGlobalObject<IConfigurationProvider>().getConfiguration<FacebookIDConfiguration>();
                if (!facebookIDconf.enabled)
                    return ActionResultTemplates<User>.UnAuthorized;

                var facebook_id = validateFacebookToken();
                if (string.IsNullOrWhiteSpace(facebook_id))
                    return ActionResultTemplates<User>.InvalidInput;
                return this.retriveFacebookAccount(facebook_id);
            }
            throw new NotImplementedException();
        }

        private ActionResult<User> retriveFacebookAccount(string id)
        {
            //live id cookies
            var cd = this.getGlobalObject<IContentDispatcher>();
            User user = cd.GetAllByFilter<UserSocialLogin>(
                x => x.provider_name == "facebook" && x.user_unique_id == id).FirstOrDefault()?.user;

            var facebookIDconf = this.getGlobalObject<IConfigurationProvider>().getConfiguration<FacebookIDConfiguration>();
            var session = this.getGlobalObject<MysterySession>();

            //first time in this instance
            if (user == null)
            {
                if (!facebookIDconf.allow_new_users)
                    return ActionResultTemplates<User>.UnAuthorized;

                var cc = this.getGlobalObject<IGlobalContentCreator>();

                if (session.authenticated_user == null)
                {
                    user = cc.getAndAddNewContent<User>();
                    user.username = "facebook:" + id;
                    user.fullname = "facebook:" + id;
                }
                else {
                    user = session.authenticated_user;
                }
                session.authenticated_user = user;
                var user_social_login = cc.getAndAddNewContent<UserSocialLogin>();
                user_social_login.user = user;
                user_social_login.user_unique_id = id;
                user_social_login.provider_name = "facebook";

            }

            session.authenticated_user = user;

            return user;
        }

        private string validateFacebookToken()
        {
            var url = "https://graph.facebook.com/me?access_token=" + input.token;
            var c = new WebClient();
            try
            {
                var json = c.DownloadString(url);
                var from_facebook = JsonConvert.DeserializeObject<FacebookToken>(json);
                if (from_facebook.id == input.id)
                    return from_facebook.id;
                else
                    return null;
            }
            catch
            {
                return null;
            }
        }

        protected override bool AuthorizeImplementation()
        {
            return true;
        }
    }
}
