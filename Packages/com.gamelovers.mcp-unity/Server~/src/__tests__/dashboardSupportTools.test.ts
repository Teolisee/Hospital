import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { ErrorType } from '../utils/errors.js';
import { registerGetConsoleLogsTool } from '../tools/getConsoleLogsTool.js';
import { registerGetScenesHierarchyTool } from '../tools/getScenesHierarchyTool.js';

const mockSendRequest = jest.fn();
const mockMcpUnity = {
  sendRequest: mockSendRequest
};

const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

const mockServerTool = jest.fn();
const mockServer = {
  tool: mockServerTool
};

describe('Dashboard Support Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('get_console_logs', () => {
    it('forwards pagination options and returns structured log data', async () => {
      registerGetConsoleLogsTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;
      const logs = [{ type: 'error', message: 'Boom' }];

      mockSendRequest.mockResolvedValue({
        success: true,
        logs
      });

      const result = await handler({
        logType: 'error',
        offset: 10,
        limit: 25,
        includeStackTrace: false
      });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'get_console_logs',
        params: {
          logType: 'error',
          offset: 10,
          limit: 25,
          includeStackTrace: false
        }
      });
      expect(result.data).toEqual({
        logs,
        offset: 10,
        limit: 25,
        logType: 'error',
        includeStackTrace: false
      });
    });
  });

  describe('get_scenes_hierarchy', () => {
    it('forwards to Unity and returns hierarchy data', async () => {
      registerGetScenesHierarchyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;
      const hierarchy = [{ name: 'SampleScene', children: [] }];

      mockSendRequest.mockResolvedValue({
        success: true,
        hierarchy
      });

      const result = await handler({});

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'get_scenes_hierarchy',
        params: {}
      });
      expect(result.content[0].text).toContain('SampleScene');
      expect(result.data).toEqual({ hierarchy });
    });

    it('throws a tool execution error when Unity fails', async () => {
      registerGetScenesHierarchyTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockResolvedValue({
        success: false,
        message: 'Hierarchy unavailable'
      });

      await expect(handler({})).rejects.toMatchObject({
        type: ErrorType.TOOL_EXECUTION,
        message: 'Hierarchy unavailable'
      });
    });
  });
});
