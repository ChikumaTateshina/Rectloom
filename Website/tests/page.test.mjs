// Checks the repository page's script against a minimal DOM.
//
// The page is the one piece of this project no Unity test can reach, and what it has
// to get right is a single URL: get the scheme wrong and the "Add to VCC" button
// silently does nothing. So the script is extracted from the page and run here.
//
// Run from anywhere:
//
//     node Website/tests/page.test.mjs
//
import fs from 'node:fs';

const pageUrl = new URL('../index.html', import.meta.url);
const html = fs.readFileSync(pageUrl, 'utf8');
// Tolerates either line ending: the working copy may be CRLF while the repository is LF.
const match = html.match(/<script>\r?\n([\s\S]*?)\r?\n<\/script>/);

if (!match) {
  console.error('could not find the page script in index.html');
  process.exit(1);
}

const script = match[1];
const canonical = 'https://chikumatateshina.github.io/Rectloom/index.json';

// The table's rows are counted from the page rather than fixed here, so that adding or removing a
// package row cannot leave the script writing into a row that does not exist.
const packagesTable = html.match(/<tbody id="packages">([\s\S]*?)<\/tbody>/);

if (!packagesTable) {
  console.error('could not find the packages table in index.html');
  process.exit(1);
}

const packageRowCount = (packagesTable[1].match(/<tr>/g) || []).length;

if (packageRowCount === 0) {
  console.error('the packages table has no rows');
  process.exit(1);
}

let failures = 0;

function check(name, actual, expected) {
  const ok = JSON.stringify(actual) === JSON.stringify(expected);

  if (ok) {
    console.log(`ok    ${name}`);
    return;
  }

  failures++;
  console.log(`FAIL  ${name}`);
  console.log(`        expected ${JSON.stringify(expected)}`);
  console.log(`        actual   ${JSON.stringify(actual)}`);
}

function makeElement(id) {
  return {
    id,
    value: '',
    textContent: '',
    dataset: {},
    listeners: {},
    addEventListener(type, handler) { this.listeners[type] = handler; },
    click() { if (this.listeners.click) { this.listeners.click(); } },
    focus() {},
    select() {},
  };
}

/**
 * Runs the page script against stub elements and returns what it did.
 */
function run({ href, listing = undefined, clipboard = true }) {
  const elements = {
    'listing-url': makeElement('listing-url'),
    'add-to-vcc': makeElement('add-to-vcc'),
    'copy-url': makeElement('copy-url'),
    'copy-status': makeElement('copy-status'),
  };

  // The page ships with the canonical URL in the field, which the script may replace.
  elements['listing-url'].value = canonical;

  const packagesBody = {
    rows: Array.from({ length: packageRowCount }, () => ({ cells: [{}, { textContent: '' }] })),
  };
  elements.packages = packagesBody;

  const assigned = [];
  const copied = [];
  const fetched = [];

  const fetchStub = (url) => {
    fetched.push(url);

    return listing
      ? Promise.resolve({ ok: true, json: () => Promise.resolve(listing) })
      : Promise.resolve({ ok: false });
  };

  const context = {
    document: { getElementById: (id) => elements[id] || null },
    location: {
      href,
      protocol: new URL(href).protocol,
      assign: (target) => assigned.push(target),
    },
    navigator: clipboard
      ? { clipboard: { writeText: (text) => { copied.push(text); return Promise.resolve(); } } }
      : {},
    window: {
      clearTimeout: () => {},
      setTimeout: () => 0,
      fetch: fetchStub,
    },
    fetch: fetchStub,
    URL,
    Object,
    Math,
    String,
    parseInt,
    isNaN,
    console,
  };

  const names = Object.keys(context);
  // eslint-disable-next-line no-new-func
  new Function(...names, script)(...names.map((name) => context[name]));

  return { elements, assigned, copied, fetched, packagesBody };
}

const settle = () => new Promise((resolve) => setTimeout(resolve, 20));

// The scheme and parameter name are what VCC registers; a typo here is the one failure
// that produces no error anywhere, just a button that does nothing.
{
  const page = run({ href: 'https://chikumatateshina.github.io/Rectloom/index.html' });
  page.elements['add-to-vcc'].click();

  check('deep link format', page.assigned, [
    'vcc://vpm/addRepo?url=https%3A%2F%2Fchikumatateshina.github.io%2FRectloom%2Findex.json',
  ]);
}

// A fork, a custom domain or a preview deployment must point at its own listing.
{
  const page = run({ href: 'https://someone.github.io/MyFork/' });

  check('fork listing url', page.elements['listing-url'].value, 'https://someone.github.io/MyFork/index.json');

  page.elements['add-to-vcc'].click();
  check('fork deep link', page.assigned, [
    'vcc://vpm/addRepo?url=https%3A%2F%2Fsomeone.github.io%2FMyFork%2Findex.json',
  ]);
}

// Opened from disk it must not offer a file path that VCC could never fetch.
{
  const page = run({ href: 'file:///C:/Rectloom/Website/index.html' });

  check('file fallback', page.elements['listing-url'].value, canonical);
}

{
  const page = run({ href: 'https://chikumatateshina.github.io/Rectloom/' });
  page.elements['copy-url'].click();

  check('copy writes the resolved url', page.copied, [canonical]);
}

// Clipboard access needs a secure context and permission, so it can simply be refused.
{
  const page = run({ href: 'https://chikumatateshina.github.io/Rectloom/', clipboard: false });
  page.elements['copy-url'].click();

  check('copy fallback explains what to do', page.elements['copy-status'].textContent.includes('Ctrl+C'), true);
}

// The script writes a version into a row by index, so the ids it looks up in the listing have to be
// the ids the table shows. Getting this wrong leaves a row reading "unpublished" forever.
{
  const ids = [...packagesTable[1].matchAll(/<code>([^<]+)<\/code>/g)].map((m) => m[1]);
  const rows = script.match(/var rows = \{([\s\S]*?)\};/);

  check('the table lists package ids', ids.length, packageRowCount);
  check('the script has a row map', rows !== null, true);

  const keys = rows === null
    ? []
    : [...rows[1].matchAll(/'([^']+)'\s*:\s*(\d+)/g)]
      .sort((a, b) => Number(a[2]) - Number(b[2]))
      .map((m) => m[1]);

  check('the row map matches the table', keys, ids);
}

// Versions come from the listing, so the page cannot drift out of date as releases happen.
{
  const listing = {
    packages: {
      // Out of order, and with a prerelease, because the page has to sort rather than take the last key.
      'com.chikumatateshina.rectloom': { versions: { '0.1.0': {}, '0.10.0': {}, '0.2.0': {}, '0.10.0-preview': {} } },
    },
  };

  const page = run({ href: 'https://chikumatateshina.github.io/Rectloom/', listing });
  await settle();

  check(
    'latest version per package',
    page.packagesBody.rows.map((row) => row.cells[1].textContent),
    ['v0.10.0'],
  );
  check('the listing is fetched once', page.fetched.length, 1);
}

// Nothing published yet, or viewed from a file: the placeholders already read correctly.
{
  const page = run({ href: 'https://chikumatateshina.github.io/Rectloom/', listing: null });
  await settle();

  check(
    'no listing leaves the placeholders',
    page.packagesBody.rows.map((row) => row.cells[1].textContent),
    Array.from({ length: packageRowCount }, () => ''),
  );
}

console.log(failures === 0 ? '\nall page checks passed' : `\n${failures} check(s) failed`);
process.exit(failures === 0 ? 0 : 1);
