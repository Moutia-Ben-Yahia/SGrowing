const fs = require('fs');
const path = require('path');

const target = path.join(__dirname, 'node_modules', '@angular', 'cli', 'src', 'utilities', 'node-version.js');
if (fs.existsSync(target)) {
  let content = fs.readFileSync(target, 'utf8');
  if (content.includes('^24.15.0')) {
    content = content.replace(/\^24\.15\.0/g, '^24.14.0');
    fs.writeFileSync(target, content, 'utf8');
    console.log('[SGrowing] Patched Angular CLI node-version.js for current Node.js runtime.');
  }
}
