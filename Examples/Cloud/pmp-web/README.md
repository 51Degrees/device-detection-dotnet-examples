# pmp-web, the Preference Management Platform (PMP) web demo

A website whose pages carry the 51Degrees Preference Management Platform
and the 51Degrees client script in every arrangement a publisher could
write them in. The PMP asks a visitor how their data may be used, and
the client script sends that answer to the cloud with everything else it
has gathered, so the 51Did the cloud creates carries the answer.

Every page is served twice. Under `/cloud/` the client script comes straight
from the cloud. Under `/pipeline/` it comes from this demo's own 51Degrees
Pipeline, which is how a website using the .NET web integration serves it.

The same pages are what the shared browser tests in
https://github.com/51Degrees/selenium-api-tests drive, in either mode. Each
language's demo will serve copies of the same pages, so that the same tests
check every language, and every language's web integration, in the same
way.

## Running it

The demo reads two environment variables, which are the names every
language's copy of it reads.

| Variable | What it holds | Also read when it is not set |
| --- | --- | --- |
| `51DEGREES_RESOURCE_KEY` | A resource key that includes the 51Did properties. Required. | `_51DEGREES_RESOURCE_KEY_51DID` |
| `51DEGREES_CLOUD_ENDPOINT` | The cloud the pages load from and the pipeline asks, given with or without the `/api/v4/` path, for example `https://cloud.51degrees.com/api/v4/`. The default is `https://cloud.51degrees.com`. | nothing |

The first column holds the names a developer sets. The resource key's second
name is the one continuous integration sets. It starts with an underscore
because a shell cannot export a name that starts with a digit, and it ends
with the product the demo needs, being the 51Did. No licence key is read,
because the resource key has to carry the 51Did product itself.

The address the demo listens on comes from `ASPNETCORE_URLS`, which is how
any ASP.NET Core application is started. Without it the demo listens on
`http://localhost:5000`.

A resource key can be created at
https://configure.51degrees.com/YldpCKbW?utm_source=github&utm_medium=readme&utm_campaign=device-detection-dotnet-examples&utm_content=examples-cloud-pmp-web-readme.md&utm_term=running-it,
which opens the Configurator with a list of properties already chosen.
The key has to carry the 51Did properties, which is what makes the cloud
include the part of the client script that asks the visitor how their data
may be used, and the pages and the browser tests read `fodid.idprobglobal`
from the answer. The key also has to carry `ThirdPartyCookiesEnabled` and
`ThirdPartyCookiesEnabledJavaScript`, because without them the PMP
never tests the third party cookie and the second card never appears. The
demo never writes the resource key or the cloud address to the console.

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

| Route, under `/cloud/` and `/pipeline/` | Carries, after the recorder and in this order |
| --- | --- |
| `common` | the PMP, then the client script |
| `common-script-first` | the client script, then the PMP |
| `change` | the PMP, the client script, then the change watcher |
| `two/one` and `two/two` | the PMP, the client script, then the change watcher, on two paths of one site |
| `consent` | the stand-in consent management platform, then the client script |
| `no-platform` | the client script alone |
| `platform-only` | the PMP alone, with no client script tag |
| `named-object` | the PMP alone with `data-object-name="fiftyOneData"`, with no client script tag |

Under `/cloud/` both the PMP and the client script load straight from
the cloud, so the cloud is a third party to the page, and the client script
posts what it gathers to the cloud's `/api/v4/json`.

Under `/pipeline/` the PMP still loads from the cloud, and the client
script is `/51Degrees.core.js` on the page's own site, served by the
51Degrees Pipeline in this demo. That script posts what it gathers to
`/51dpipeline/json` on the same site, and the pipeline asks the cloud with
its cloud request engine, then turns the answer into device and 51Did data
with `DeviceDetectionCloudEngine` and `DidCloudEngine`. The pipeline's
elements are listed in `appsettings.json`, and it runs only for these pages
and those two requests.

Two things differ under `/pipeline/`, and both are deliberate.

1. **The client script tag is not `async`.** The PMP looks for the client
   script's page object, `fod` or the name `data-object-name` gives, and
   waits for it until the page has loaded before adding a client script of
   its own, so a script served by the pipeline at `/51Degrees.core.js` is
   found like any other. The tag was made synchronous while the PMP
   recognised a client script tag only by the cloud's address, and it can
   go back to `async` once the browser tests have been run against that.
2. **The pages with no client script tag get the cloud's client script.** The
   PMP builds the address of the script it adds from the cloud that
   served the PMP, so `/pipeline/platform-only` and
   `/pipeline/named-object` behave exactly as their `/cloud/` copies.

The PMP tag carries the resource key as the file name in its `src`, which
is where the PMP reads it from, and these attributes on every page, in this
order. The demo builds the whole `src` and the template carries it as one
placeholder, so the key is escaped for a URL path in one place.

| Attribute | Value |
| --- | --- |
| `src` | `{{PMP_SCRIPT_URL}}`, the cloud, then `/api/v4/pmp/`, the resource key escaped for a path and `.js` |
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
2. `wwwroot/templates/cloud/<route>.html` and
   `wwwroot/templates/pipeline/<route>.html` hold one plain HTML page for
   each route above in each mode, with placeholders where the values go. A
   pipeline page is its cloud copy with the client script tag changed as
   described above and one sentence added saying where the script comes
   from.
3. `wwwroot/index.html` lists the pages.

The templates use these two placeholders, and both are finished addresses.
A template never joins an address together out of parts, because the
resource key sits in the path of both of them and a placeholder is only
ever encoded for HTML, which leaves `/`, `?`, `#` and `%` as they were. The
demo escapes the key for a path where it builds each address, in
`Settings.cs`.

| Placeholder | Filled with | Example |
| --- | --- | --- |
| `{{PMP_SCRIPT_URL}}` | the address of the PMP, being the cloud's address, then `/api/v4/pmp/`, then the resource key escaped for a path, then `.js`, under both `/cloud/` and `/pipeline/` | `https://cloud.51degrees.com/api/v4/pmp/<your resource key>.js` |
| `{{CLIENT_SCRIPT_URL}}` | the address of the client script, which under `/cloud/` is the cloud's address, then `/api/v4/`, then the resource key escaped for a path, then `.js`, and under `/pipeline/` is `/51Degrees.core.js` | `https://cloud.51degrees.com/api/v4/<your resource key>.js` |

`{{RESOURCE_KEY}}` and `{{CLOUD_ENDPOINT}}` are still filled, holding the
key and the cloud's address on their own, because the set of names is
shared with the other languages' copies of this demo. No template here uses
them, and dropping them is a change to make in all the copies at once.

The client script's object name is not a placeholder. The only page that
names an object, `/cloud/named-object`, names `fiftyOneData`, which the
tests look for, and every other page uses the default `fod`, so the names
are written into the templates and a filler has nothing to supply.

A copy in another language follows these rules, which are all that
`Program.cs`, `Settings.cs` and `Pages.cs` do here.

1. Read the two environment variables above, with the same fallback, take
   `/api/v4` and any trailing slash off the endpoint, and build the two
   addresses in the placeholder table from it, escaping the resource key
   for a URL path segment in each.
2. Serve `wwwroot` as static files, with `index.html` at `/`.
3. Answer `/cloud/<route>` with `wwwroot/templates/cloud/<route>.html`, and
   `/pipeline/<route>` with `wwwroot/templates/pipeline/<route>.html`,
   replacing each placeholder with its value encoded for an HTML
   attribute. That encoding is the last step and not the only one: an
   address holding the resource key is escaped for a URL path where it is
   built, in step 1, because HTML encoding does not do that job. A route is
   lower case letters, digits and hyphens, with a slash between parts.
   Anything else, or a route with no template, answers 404.
4. Refuse to serve a page that still holds `{{` once it is filled, so a
   misspelt placeholder fails where it is rather than in a test.
5. Send `Cache-Control: no-store, no-cache, must-revalidate` with every
   page and script.
6. Answer every host name.
7. Never write the resource key to a console or a log.
8. Run the language's own 51Degrees web integration for the pages under
   `/pipeline/`, with a cloud request engine given the same resource key and
   cloud, the device detection and 51Did cloud engines, and client side
   evidence turned on, so it serves the client script at
   `/51Degrees.core.js` and its JSON where that integration serves it. The
   JavaScript builder has to carry the template with the user prompt block,
   which renders only where the 51Did engine reports properties the
   resource key is entitled to.

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
