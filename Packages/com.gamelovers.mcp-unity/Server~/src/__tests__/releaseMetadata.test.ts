import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const serverDirectory = process.cwd();
const repositoryDirectory = join(serverDirectory, '..');

describe('release 1.4.0 metadata', () => {
  it('publishes 1.4.0 from both package manifests and the MCP protocol', () => {
    const unityPackage = JSON.parse(readFileSync(join(repositoryDirectory, 'package.json'), 'utf8'));
    const nodePackage = JSON.parse(readFileSync(join(serverDirectory, 'package.json'), 'utf8'));
    const lockfile = JSON.parse(readFileSync(join(serverDirectory, 'package-lock.json'), 'utf8'));
    const serverSource = readFileSync(join(serverDirectory, 'src', 'index.ts'), 'utf8');
    const dashboardSource = readFileSync(join(serverDirectory, 'src', 'ui', 'unity-dashboard.html'), 'utf8');

    expect(unityPackage.version).toBe('1.4.0');
    expect(unityPackage.unity).toBe('2022.3');
    expect(nodePackage.version).toBe('1.4.0');
    expect(lockfile.version).toBe('1.4.0');
    expect(lockfile.packages[''].version).toBe('1.4.0');
    expect(serverSource).toContain('version: "1.4.0"');
    expect(dashboardSource).toContain("appInfo: { name: 'unity-dashboard', version: '1.4.0' }");
  });

  it('does not keep a registry manifest for an unpublished npm package', () => {
    expect(existsSync(join(repositoryDirectory, 'server.json'))).toBe(false);
  });
});
