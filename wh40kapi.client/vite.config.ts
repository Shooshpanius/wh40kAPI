import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import { env } from 'process';

// Prefer HTTP back-end URL. If ASPNETCORE_URLS is set use its first entry,
// otherwise fall back to the default development backend address.
const target = env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'http://localhost:5264';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false
            },
            '^/scalar': {
                target,
                secure: false
            },
            '^/openapi': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '51018'),
        // Disable HTTPS for the Vite dev server and use plain HTTP
        //https: false
    }
})
