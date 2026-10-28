import { defineConfig, devices } from '@playwright/test';

const CI = !!process.env.CI;

export default defineConfig({
    testDir: './testler',
    fullyParallel: true,
    forbidOnly: CI,
    retries: CI ? 1 : 0,
    timeout: 45_000,
    reporter: CI ? [['list'], ['html', { open: 'never' }]] : 'list',
    use: {
        baseURL: 'http://127.0.0.1:5500',
        // sw.js istekleri önbellekten sunup sahte API'yi atlatmasın
        serviceWorkers: 'block',
        locale: 'tr-TR',
        trace: 'retain-on-failure',
    },
    projects: [
        { name: 'sahte-masaustu', grep: /@sahte/, use: { ...devices['Desktop Chrome'] } },
        { name: 'sahte-mobil', grep: /@sahte/, use: { ...devices['Pixel 7'] } },
    ],
    webServer: {
        command: 'node sunucu.mjs',
        url: 'http://127.0.0.1:5500/index.html',
        reuseExistingServer: !CI,
    },
});
