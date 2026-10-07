#!/usr/bin/env python3
"""
HalconVisionStudio MCP Stdio Bridge
供 Claude Code CLI, Cursor, Windsurf, Codex 等基于 Stdio 的 MCP Client 连接使用。

配置（按优先级）：
  - HALCON_MCP_PORT   环境变量：MCP Server 端口（默认 8765，需与软件 AI 面板中的端口一致）
  - HALCON_MCP_TOKEN  环境变量：MCP 访问令牌
  - 若未设置令牌环境变量，将自动读取 %APPDATA%\\HalconVisionStudio\\mcp_token.txt
    （该文件由 HalconVisionStudio 启动 MCP 服务时自动生成）

使用方式:
  claude mcp add halcon-studio -- python d:/Code/AAAandSphinx/halcon-vison-studio/halcon-mcp-bridge.py
"""

import sys
import os
import json
import urllib.request
import urllib.error


def resolve_port():
    try:
        return int(os.environ.get("HALCON_MCP_PORT", "8765"))
    except ValueError:
        return 8765


def resolve_token():
    token = os.environ.get("HALCON_MCP_TOKEN", "").strip()
    if token:
        return token

    # 自动读取软件生成的令牌文件（同机部署时的默认方式）
    appdata = os.environ.get("APPDATA", "")
    token_file = os.path.join(appdata, "HalconVisionStudio", "mcp_token.txt")
    try:
        with open(token_file, "r", encoding="utf-8") as f:
            return f.read().strip()
    except OSError:
        return ""


MCP_PORT = resolve_port()
MCP_TOKEN = resolve_token()
MCP_SERVER_URL = f"http://127.0.0.1:{MCP_PORT}/message"


def main():
    sys.stderr.write(f"[Halcon-MCP-Bridge] Bridge started. Connecting to {MCP_SERVER_URL}"
                     f" (token: {'loaded' if MCP_TOKEN else 'MISSING'})\n")
    sys.stderr.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue

        try:
            req_data = line.encode("utf-8")
            headers = {"Content-Type": "application/json; charset=utf-8"}
            if MCP_TOKEN:
                headers["Authorization"] = "Bearer " + MCP_TOKEN

            req = urllib.request.Request(MCP_SERVER_URL, data=req_data, headers=headers, method="POST")

            with urllib.request.urlopen(req, timeout=60) as resp:
                status = resp.status
                if status == 200:
                    resp_body = resp.read().decode("utf-8")
                    if resp_body:
                        sys.stdout.write(resp_body + "\n")
                        sys.stdout.flush()
                elif status == 204:
                    # No content (notification)
                    pass
                elif status == 202:
                    # Accepted (notification)
                    pass

        except urllib.error.HTTPError as e:
            # 401 = 令牌错误/缺失
            try:
                rpc_req = json.loads(line)
                req_id = rpc_req.get("id")
            except Exception:
                req_id = None

            err_resp = {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {
                    "code": -32002,
                    "message": f"MCP Server 拒绝访问 (HTTP {e.code})。请检查令牌：设置环境变量 HALCON_MCP_TOKEN，"
                               f"或确认 %APPDATA%\\HalconVisionStudio\\mcp_token.txt 与软件中的令牌一致。"
                }
            }
            sys.stdout.write(json.dumps(err_resp) + "\n")
            sys.stdout.flush()

        except urllib.error.URLError as e:
            try:
                rpc_req = json.loads(line)
                req_id = rpc_req.get("id")
            except Exception:
                req_id = None

            err_resp = {
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {
                    "code": -32001,
                    "message": f"HalconVisionStudio 未运行或未启用内置 MCP 服务 (地址: {MCP_SERVER_URL})。"
                               f"请先启动软件并开启 AI 服务。错误详情: {str(e)}"
                }
            }
            sys.stdout.write(json.dumps(err_resp) + "\n")
            sys.stdout.flush()

        except Exception as e:
            sys.stderr.write(f"[Halcon-MCP-Bridge] Error: {str(e)}\n")
            sys.stderr.flush()


if __name__ == "__main__":
    main()
