﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Validation;
using Microsoft.Extensions.Options;
using Volo.Abp.Text.Formatting;

namespace Volo.Abp.IdentityServer
{
    public class AbpStrictRedirectUriValidator : StrictRedirectUriValidator
    {
        protected AbpRedirectUriValidatorOptions Options { get; }

        public AbpStrictRedirectUriValidator(IOptions<AbpRedirectUriValidatorOptions> options)
        {
            Options = options.Value;
        }

        public override async Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client)
        {
            var isAllowed = await base.IsRedirectUriValidAsync(requestedUri, client);
            return isAllowed || await IsRedirectUriValidWithDomainFormatsAsync(client.RedirectUris, requestedUri);
        }

        public override async Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client)
        {
            var isAllowed = await base.IsPostLogoutRedirectUriValidAsync(requestedUri, client);
            return isAllowed || await IsRedirectUriValidWithDomainFormatsAsync(client.PostLogoutRedirectUris, requestedUri);
        }

        protected virtual Task<bool> IsRedirectUriValidWithDomainFormatsAsync(IEnumerable<string> uris, string requestedUri)
        {
            if (uris == null)
            {
                return Task.FromResult(false);
            }

            foreach (var url in uris)
            {
                if (Options.DomainFormats.Any(x => url.Contains(x)))
                {
                    var extractResult = FormattedStringValueExtracter.Extract(requestedUri, url, ignoreCase: true);
                    if (extractResult.IsMatch)
                    {
                        return Task.FromResult(true);
                    }
                }
            }

            return Task.FromResult(false);
        }
    }
}
