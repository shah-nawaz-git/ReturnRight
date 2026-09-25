import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  images: {},
  experimental: {
    // Above the API's 10 MB upload cap + multipart overhead so the proxy streams
    // the whole body and the API returns its own friendly 400/413.
    proxyClientMaxBodySize: 12 * 1024 * 1024,
  },
  async headers() {
    // No CSP for V1 — Next renders inline scripts that a strict CSP would break.
    return [
      {
        source: "/:path*",
        headers: [
          { key: "X-Content-Type-Options", value: "nosniff" },
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          { key: "X-Frame-Options", value: "DENY" },
          // camera=(self): photo evidence capture; mic/geo unused.
          {
            key: "Permissions-Policy",
            value: "camera=(self), microphone=(), geolocation=()",
          },
        ],
      },
    ];
  },
  async rewrites() {
    return [
      {
        source: "/api/:path*",
        destination: `${process.env.API_INTERNAL_URL ?? "http://localhost:5080"}/api/:path*`,
      },
    ];
  },
};

export default nextConfig;
