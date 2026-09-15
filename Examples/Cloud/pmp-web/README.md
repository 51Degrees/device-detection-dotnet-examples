# pmp-web, the Preference Management Platform (PMP) web demo

A website whose pages carry the 51Degrees Preference Management Platform
and the 51Degrees client script in every arrangement a publisher could
write them in. The platform asks a visitor how their data may be used, and
the client script sends that answer to the cloud with everything else it
has gathered, so the 51Did the cloud creates carries the answer.

The same pages are what the shared browser tests in
https://github.com/51Degrees/selenium-api-tests drive. Each language's
demo will serve copies of the same pages, so that the same tests check
every language in the same way.

## Running it

The demo reads two environment variables, which are the names every
language's copy of it reads.

| Variable | What it holds | Also read when it is not set |
| --- | --- | --- |
| `51DEGREES_RESOURCE_KEY` | A resource key that includes the 51Did properties. Required. | `_51DEGREES_RESOURCE_KEY_51DID` |
| `51DEGREES_CLOUD_ENDPOINT` | The cloud the pages load from, given with or without the `/api/v4/` path, for example `https://cloud.51degrees.com/api/v4/`. The default is `https://cloud.51degrees.com`. | nothing |

The first column holds the names a developer sets. The resource key's second
name is the one continuous integration sets. It starts with an underscore
because a shell cannot export a name that starts with a digit, and it ends
with the product the demo needs, being the 51Did. No licence key is read,
because the resource key has to carry the 51Did product itself.

The address the demo listens on comes from `ASPNETCORE_URLS`, which is how
any ASP.NET Core application is started. Without it the demo listens on
`http://localhost:5000`.

A resource key can be created at
https://configure.51degrees.com?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-pmp-web-readme.md&utm_term=running-it.
The demo never writes either value to the console.

On Linux and macOS the variable names that start with a digit have to be
set with the env command, because POSIX shells do not accept them in a
plain assignment.

```
env 51DEGREES_RESOURCE_KEY=<your resource key> \
    ASPNETCORE_URLS=http://localhost:5180 \
    dotnet run -c Release
```

In PowerShell the same is done like this.

```
${env:51DEGREES_RESOURCE_KEY} = '<your resource key>'
${env:ASPNETCORE_URLS} = 'http://localhost:5180'
dotnet run -c Release
```

Then open http://localhost:5180 for a list of the pages.

## The pages

Every page starts with the recorder, and every page answers on any host
name. The browser tests open the same page as `site-a.localtest` and
`site-b.localtest` on one port, with the cloud on a third name, so that an
answer shared between two sites can be checked.

| Route | Carries, after the recorder and in this order |
| --- | --- |
| `/cloud/common` | the platform, then the client script |
| `/cloud/common-script-first` | the client script, then the platform |
| `/cloud/change` | the platform, the client script, then the change watcher |
| `/cloud/two/one` and `/cloud/two/two` | the platform, the client script, then the change watcher, on two paths of one site |
| `/cloud/consent` | the stand-in consent management platform, then the client script |
| `/cloud/no-platform` | the client script alone |
| `/cloud/platform-only` | the platform alone, with no client script tag |
| `/cloud/named-object` | the platform alone with `data-object-name="fiftyOneData"`, with no client script tag |

Under `/cloud/` both the platform and the client script load straight from
the cloud, so the cloud is a third party to the page.

The platform tag carries these attributes on every page, in this order.

| Attribute | Value |
| --- | --- |
| `src` | the cloud, then `/api/v4/pmp` |
| `data-resource-key` | the resource key |
| `data-action-url` | `javascript:window.__51dTest.actions.push('{preference}')` |
| `data-tcf-vendor` | `CPYBSvoPYBSvoO3AAAENAwCAAAAAAAAAAAAAAAAAAAAA` |
| `data-brand-name` | `Fifty One Times` |
| `data-brand-terms-url` | `https://example.com/privacy` |
| `data-alt-name` | `Subscribe` |
| `data-alt-url` | `javascript:window.__51dTest.altFired = true` |
| `data-show-standard` | `true` |
| `data-network-name` | `Fifty One Network` |
| `data-object-name` | `fiftyOneData`, on `/cloud/named-object` only |

## How it is built, so another language can copy it

Everything the tests rely on is in plain files under `wwwroot`, and the
program does as little as possible, so that another language copies
`wwwroot` and writes only a small page filler.

1. `wwwroot/js` holds three scripts, served unchanged.
   - `recorder.js` is the first script on every page. It records into
     `window.__51dTest` every request made with `XMLHttpRequest` or
     `fetch`, with its method, address, body, status and response, and
     every console line, so a test can read what the page sent and logged.
   - `change-watcher.js` registers a handler with `fod.onChange` as soon as
     `fod` exists and records each change into `window.__51dTest.changes`.
   - `consent-stub.js` is a stand-in consent management platform
     answering `__tcfapi`. It delivers a consent string granting purposes
     1 to 12 only when `window.__51dCmp.deliver()` is called.
2. `wwwroot/templates/cloud/<route>.html` holds one plain HTML page for
   each route above, with placeholders where the values go.
3. `wwwroot/index.html` lists the pages.

The placeholders are these three, and every template uses exactly these
names.

| Placeholder | Filled with | Example |
| --- | --- | --- |
| `{{RESOURCE_KEY}}` | the resource key | `<your resource key>` |
| `{{CLOUD_ENDPOINT}}` | the cloud's address with no trailing slash and without `/api/v4` | `https://cloud.51degrees.com` |
| `{{CLIENT_SCRIPT_URL}}` | the address of the client script, which under `/cloud/` is the cloud's address, then `/api/v4/`, then the resource key, then `.js` | `https://cloud.51degrees.com/api/v4/<your resource key>.js` |

The client script's object name is not a placeholder. The only page that
names an object, `/cloud/named-object`, names `fiftyOneData`, which the
tests look for, and every other page uses the default `fod`, so the names
are written into the templates and a filler has nothing to supply.

A copy in another language follows these rules, which are all that
`Program.cs`, `Settings.cs` and `Pages.cs` do here.

1. Read the two environment variables above, with the same fallback, and
   take `/api/v4` and any trailing slash off the endpoint.
2. Serve `wwwroot` as static files, with `index.html` at `/`.
3. Answer `/cloud/<route>` with `wwwroot/templates/cloud/<route>.html`,
   replacing each placeholder with its value encoded for an HTML
   attribute. A route is lower case letters, digits and hyphens, with a
   slash between parts. Anything else, or a route with no template,
   answers 404.
4. Refuse to serve a page that still holds `{{` once it is filled, so a
   misspelt placeholder fails where it is rather than in a test.
5. Send `Cache-Control: no-store, no-cache, must-revalidate` with every
   page and script.
6. Answer every host name.
7. Never write the resource key to a console or a log.

The links under "Find out more" on each page carry this repository's name
as their `utm_campaign`, which a copy changes to its own repository's name.

## Find out more

- The 51Did and the Preference Management Platform,
  https://51degrees.com/documentation/_identifiers__index.html?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-pmp-web-readme.md&utm_term=find-out-more
- Talk to 51Degrees about using them,
  https://51degrees.com/contact-us?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-pmp-web-readme.md&utm_term=find-out-more
- The source code for this demo and what it uses.
  - https://github.com/51Degrees/device-detection-dotnet-examples
  - https://github.com/51Degrees/pipeline-dotnet
  - https://github.com/51Degrees/javascript-templates
  - https://github.com/51Degrees/specifications
  - https://github.com/51Degrees/selenium-api-tests
