/**
 * Dev-server proxy: everything under /api is forwarded to the backend, so the SPA makes
 * same-origin calls and CORS never comes into it during development.
 *
 * A .js file rather than .json purely so the target can come from the environment. Under
 * docker-compose the API is reachable as http://api:5211 — a container cannot see the host's
 * localhost — while `npm start` on the host needs localhost:5211. One file, both cases.
 */
module.exports = {
  '/api': {
    target: process.env['API_PROXY_TARGET'] || 'http://localhost:5211',
    secure: false,
    changeOrigin: true,
  },
};
