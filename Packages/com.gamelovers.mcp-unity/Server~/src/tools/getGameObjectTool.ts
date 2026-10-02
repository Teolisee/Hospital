import * as z from "zod";
import { Logger } from "../utils/logger.js";
import { McpUnity } from "../unity/mcpUnity.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { McpUnityError, ErrorType } from "../utils/errors.js";
import { CallToolResult } from "@modelcontextprotocol/sdk/types.js";

// Constants for the tool
const toolName = "get_gameobject";
const toolDescription =
  "Retrieves detailed information about a specific GameObject by instance ID, name, or hierarchical path (e.g., 'Parent/Child/MyObject'). Returns component info plus a scoped child hierarchy. Use 'maxDepth', 'includeComponents', and 'includeComponentProperties' to control response size and avoid token/response limits. If a node carries '_truncated: true' with reason 'depth_limit' or 'size_limit_exceeded', re-query that node directly with narrower parameters.";
const paramsSchema = z.object({
  idOrName: z
    .string()
    .describe(
      "The instance ID (integer), name, or hierarchical path of the GameObject to retrieve. Use hierarchical paths like 'Canvas/Panel/Button' for nested objects."
    ),
  maxDepth: z
    .number()
    .int()
    .min(0)
    .max(50)
    .optional()
    .describe(
      "Maximum child hierarchy depth to traverse. 0 = no children, 1 = direct children, 2 = grandchildren. Default: 2. Increase only when you actually need a deeper tree — large scenes can exceed the MCP 15MB response cap."
    ),
  includeComponents: z
    .boolean()
    .optional()
    .describe(
      "Include the component list on each node. Set false to get a hierarchy-only outline. Default: true."
    ),
  includeComponentProperties: z
    .boolean()
    .optional()
    .describe(
      "Include serialized property values for each component. Set false to keep component type names only (saves substantial tokens). Default: true."
    ),
});

/**
 * Creates and registers the Get GameObject tool with the MCP server
 * This tool allows retrieving detailed information about GameObjects in Unity scenes
 *
 * @param server The MCP server instance to register with
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param logger The logger instance for diagnostic information
 */
export function registerGetGameObjectTool(
  server: McpServer,
  mcpUnity: McpUnity,
  logger: Logger
) {
  logger.info(`Registering tool: ${toolName}`);

  // Register this tool with the MCP server
  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: z.infer<typeof paramsSchema>) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

/**
 * Handles requests for GameObject information from Unity
 *
 * @param mcpUnity The McpUnity instance to communicate with Unity
 * @param params The parameters for the tool
 * @returns A promise that resolves to the tool execution result
 * @throws McpUnityError if the request to Unity fails
 */
async function toolHandler(
  mcpUnity: McpUnity,
  params: z.infer<typeof paramsSchema>
): Promise<CallToolResult> {
  const { idOrName, maxDepth, includeComponents, includeComponentProperties } = params;

  // Send request to Unity
  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      idOrName,
      maxDepth,
      includeComponents,
      includeComponentProperties,
    },
  });

  if (!response.success) {
    throw new McpUnityError(
      ErrorType.TOOL_EXECUTION,
      response.message || "Failed to fetch GameObject from Unity"
    );
  }

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify(response, null, 2),
      },
    ],
  };
}


