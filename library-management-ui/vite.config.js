import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import process from 'node:process'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    plugins: [react()],
    server: {
      proxy: {
        '/api': {
          target: env.VITE_API_PROXY_TARGET || 'http://localhost:5154',
          changeOrigin: true,
          secure: false,
          configure: (proxy) => {
            // The API sees Vite as its TCP peer. Forward Vite's actual peer
            // address so login auditing records the browser client address.
            proxy.on('proxyReq', (proxyRequest, request) => {
              let clientIp = request.socket.remoteAddress
              if (!clientIp) return
              if (clientIp.startsWith('::ffff:')) clientIp = clientIp.slice(7)
              proxyRequest.setHeader('X-Forwarded-For', clientIp)
              proxyRequest.setHeader('X-Real-IP', clientIp)
            })
            proxy.on('error', (err) => {
              console.error('API Proxy Error:', err.message)
            })
          }
        }
      }
    }
  }
})
