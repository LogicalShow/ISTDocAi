import fs from 'node:fs';
import path from 'node:path';
import { APP_ICONS } from '../src/app/shared/icons/app-icons';

const iconsDirectory = path.resolve(__dirname, './icon-sources/ui');
const outputFile = path.resolve(__dirname, '../src/assets/icons/app-icons.svg');

function findSvg(iconName: string): string {
  const candidate = path.join(iconsDirectory, `${iconName}.svg`);

  if (!fs.existsSync(candidate)) {
    throw new Error(`Icon "${iconName}" was not found at ${candidate}`);
  }

  return candidate;
}

const seen = new Set<string>();
const symbols: string[] = [];

for (const iconName of APP_ICONS) {
  if (seen.has(iconName)) {
    throw new Error(`Duplicate icon name in APP_ICONS: "${iconName}"`);
  }
  seen.add(iconName);

  const filePath = findSvg(iconName);
  const svg = fs.readFileSync(filePath, 'utf8');

  const viewBoxMatch = svg.match(/viewBox=["']([^"']+)["']/i);
  if (!viewBoxMatch) {
    throw new Error(`Icon "${iconName}" has no viewBox (${filePath})`);
  }

  const contentMatch = svg.match(/<svg[^>]*>([\s\S]*)<\/svg>/i);
  if (!contentMatch) {
    throw new Error(`Invalid SVG: "${iconName}" (${filePath})`);
  }

  const viewBox = viewBoxMatch[1];
  const content = contentMatch[1].trim();

  symbols.push(`  <symbol id="${iconName}" viewBox="${viewBox}">\n    ${content}\n  </symbol>`);
}

const sprite = `<svg xmlns="http://www.w3.org/2000/svg">\n${symbols.join('\n')}\n</svg>\n`;

fs.mkdirSync(path.dirname(outputFile), { recursive: true });
fs.writeFileSync(outputFile, sprite, 'utf8');

console.log(`Generated ${APP_ICONS.length} icons -> ${outputFile}`);
