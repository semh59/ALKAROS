import path from "node:path";
const repositoryRoot = "D:/PROJECT/ALKAROS";
export default { root: path.join(repositoryRoot, "evidence/V1-RMD-029/browser-harness"), resolve: { alias: { react: path.join(repositoryRoot, "src/Clients/PosTerminal/node_modules/react"), "react-dom": path.join(repositoryRoot, "src/Clients/PosTerminal/node_modules/react-dom") } }, server: { host: "127.0.0.1", port: 58329, strictPort: true } };
