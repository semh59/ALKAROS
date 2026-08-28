import path from "node:path";

const repositoryRoot = "D:/PROJECT/ALKAROS";

export default {
  root: path.join(repositoryRoot, "evidence/V1-RMD-028/browser-harness"),
  resolve: {
    alias: {
      react: path.join(repositoryRoot, "src/Clients/PosTerminal/node_modules/react"),
      "react-dom": path.join(repositoryRoot, "src/Clients/PosTerminal/node_modules/react-dom"),
    },
  },
  server: { host: "127.0.0.1", port: 58328, strictPort: true },
};
