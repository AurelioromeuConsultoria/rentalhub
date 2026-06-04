import { readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const command = process.argv[2];
const isDryRun = process.argv.includes('--dry-run');

const files = {
  rootPackage: 'package.json',
  adminPackage: 'apps/Admin/package.json',
  adminLock: 'apps/Admin/package-lock.json',
  adminVersion: 'apps/Admin/src/lib/version.js',
  apiProject: 'apps/API/src/RentalHub.API/RentalHub.API.csproj',
  sistemaController: 'apps/API/src/RentalHub.API/Controllers/SistemaController.cs',
  supportRoutine: 'docs/ROTINA_SUPORTE_ATUALIZACOES.md',
};

function usage() {
  console.log(`Uso:
  npm run version:check
  npm run version:sync
  npm run release:patch
  npm run release:minor
  npm run release:major

Opcoes:
  --dry-run  Mostra a proxima versao sem escrever arquivos`);
}

function assertVersion(version) {
  if (!/^\d+\.\d+\.\d+$/.test(version)) {
    throw new Error(`Versao invalida: ${version}`);
  }
}

function bump(version, type) {
  const [major, minor, patch] = version.split('.').map(Number);

  if (type === 'major') return `${major + 1}.0.0`;
  if (type === 'minor') return `${major}.${minor + 1}.0`;
  if (type === 'patch') return `${major}.${minor}.${patch + 1}`;

  throw new Error(`Comando invalido: ${type}`);
}

async function readText(relativePath) {
  return readFile(path.join(rootDir, relativePath), 'utf8');
}

async function writeText(relativePath, content) {
  if (isDryRun) return;
  await writeFile(path.join(rootDir, relativePath), content);
}

async function readJson(relativePath) {
  return JSON.parse(await readText(relativePath));
}

async function writeJson(relativePath, data) {
  await writeText(relativePath, `${JSON.stringify(data, null, 2)}\n`);
}

async function getCurrentVersion() {
  const content = await readText(files.adminVersion);
  const match = content.match(/APP_VERSION = '([^']+)'/);

  if (!match) {
    throw new Error(`Nao foi possivel ler APP_VERSION em ${files.adminVersion}`);
  }

  assertVersion(match[1]);
  return match[1];
}

async function collectVersions() {
  const rootPackage = await readJson(files.rootPackage);
  const adminPackage = await readJson(files.adminPackage);
  const adminLock = await readJson(files.adminLock);
  const adminVersion = await getCurrentVersion();
  const apiProject = await readText(files.apiProject);
  const sistemaController = await readText(files.sistemaController);

  return {
    [files.rootPackage]: rootPackage.version,
    [files.adminPackage]: adminPackage.version,
    [`${files.adminLock}#version`]: adminLock.version,
    [`${files.adminLock}#packages.root`]: adminLock.packages?.['']?.version,
    [files.adminVersion]: adminVersion,
    [`${files.apiProject}#Version`]: apiProject.match(/<Version>([^<]+)<\/Version>/)?.[1],
    [`${files.apiProject}#InformationalVersion`]: apiProject.match(/<InformationalVersion>([^<]+)<\/InformationalVersion>/)?.[1],
    [files.sistemaController]: sistemaController.match(/AdminVersion = "([^"]+)"/)?.[1],
  };
}

async function checkVersions() {
  const versions = await collectVersions();
  const entries = Object.entries(versions);
  const missing = entries.filter(([, version]) => !version);
  const uniqueVersions = [...new Set(entries.map(([, version]) => version).filter(Boolean))];

  if (missing.length > 0) {
    throw new Error(`Versao nao encontrada em:\n${missing.map(([file]) => `- ${file}`).join('\n')}`);
  }

  if (uniqueVersions.length > 1) {
    throw new Error(`Versoes divergentes:\n${entries.map(([file, version]) => `- ${file}: ${version}`).join('\n')}`);
  }

  console.log(`Versao sincronizada: ${uniqueVersions[0]}`);
}

async function updateVersion(nextVersion) {
  assertVersion(nextVersion);

  const rootPackage = await readJson(files.rootPackage);
  rootPackage.version = nextVersion;
  await writeJson(files.rootPackage, rootPackage);

  const adminPackage = await readJson(files.adminPackage);
  adminPackage.version = nextVersion;
  await writeJson(files.adminPackage, adminPackage);

  const adminLock = await readJson(files.adminLock);
  adminLock.version = nextVersion;
  if (adminLock.packages?.['']) {
    adminLock.packages[''].version = nextVersion;
  }
  await writeJson(files.adminLock, adminLock);

  await writeText(files.adminVersion, `export const APP_VERSION = '${nextVersion}';\n`);

  const assemblyVersion = `${nextVersion}.0`;
  const apiProject = (await readText(files.apiProject))
    .replace(/<Version>[^<]+<\/Version>/, `<Version>${nextVersion}</Version>`)
    .replace(/<AssemblyVersion>[^<]+<\/AssemblyVersion>/, `<AssemblyVersion>${assemblyVersion}</AssemblyVersion>`)
    .replace(/<FileVersion>[^<]+<\/FileVersion>/, `<FileVersion>${assemblyVersion}</FileVersion>`)
    .replace(/<InformationalVersion>[^<]+<\/InformationalVersion>/, `<InformationalVersion>${nextVersion}</InformationalVersion>`);
  await writeText(files.apiProject, apiProject);

  const sistemaController = (await readText(files.sistemaController))
    .replace(/AdminVersion = "[^"]+"/, `AdminVersion = "${nextVersion}"`);
  await writeText(files.sistemaController, sistemaController);

  const supportRoutine = (await readText(files.supportRoutine))
    .replace(/A versao incremental atual do produto e `[^`]+`\./, `A versao incremental atual do produto e \`${nextVersion}\`.`)
    .replace(
      /Ao publicar nova versao:[\s\S]*?## Migrations/,
      `Ao publicar nova versao:\n\n1. Rodar \`npm run release:patch\`, \`npm run release:minor\` ou \`npm run release:major\` na raiz do repositorio.\n2. Rodar \`npm run version:check\` para confirmar que Admin, API e metadados estao sincronizados.\n3. Validar build e testes antes do deploy.\n4. Configurar aviso de atualizacao em Configuracoes quando o cliente precisar ser informado.\n\n## Migrations`
    );
  await writeText(files.supportRoutine, supportRoutine);

  console.log(`${isDryRun ? '[dry-run] ' : ''}Versao atualizada para ${nextVersion}`);
}

async function main() {
  if (!command || command === 'help' || command === '--help') {
    usage();
    return;
  }

  const currentVersion = await getCurrentVersion();

  if (command === 'check') {
    await checkVersions();
    return;
  }

  if (command === 'sync') {
    await updateVersion(currentVersion);
    if (!isDryRun) {
      await checkVersions();
    }
    return;
  }

  if (!['patch', 'minor', 'major'].includes(command)) {
    throw new Error(`Comando invalido: ${command}`);
  }

  await updateVersion(bump(currentVersion, command));
  if (!isDryRun) {
    await checkVersions();
  }
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
