import { execFileSync } from 'node:child_process';
import { copyFileSync } from 'node:fs';

export default function globalSetup() {
  execFileSync('dotnet', [
    'run', '--project', 'tests/ui-layout/FixtureRenderer/FixtureRenderer.csproj',
    '--configuration', 'Debug', '--no-launch-profile', '-p:BuildInParallel=false'
  ], { cwd: process.cwd(), stdio: 'inherit', timeout: 120_000 });
  const output = 'tests/ui-layout/FixtureRenderer/bin/Debug/net10.0';
  copyFileSync('Aurora.App/wwwroot/app.css', `${output}/app.css`);
  copyFileSync('Aurora.App/Components/Layout/MainLayout.razor', `${output}/MainLayout.razor`);
  for (const component of ['CharacterShopWorkspace', 'CharacterEquipmentWorkspace']) {
    copyFileSync(`Aurora.Components/obj/Debug/net10.0/scopedcss/Shared/${component}.razor.rz.scp.css`, `${output}/${component}.css`);
  }
}
