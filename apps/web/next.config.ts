import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone",
  images: {},
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
