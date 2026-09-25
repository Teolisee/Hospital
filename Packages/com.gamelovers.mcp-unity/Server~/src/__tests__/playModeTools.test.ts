import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { ErrorType, McpUnityError } from '../utils/errors.js';
import { registerGetPlayModeStatusTool } from '../tools/getPlayModeStatusTool.js';
import { registerSetPlayModeStatusTool } from '../tools/setPlayModeStatusTool.js';

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

describe('Play Mode Tools', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('get_play_mode_status', () => {
    it('registers the tool with the MCP server', () => {
      registerGetPlayModeStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledWith(
        'get_play_mode_status',
        expect.any(String),
        expect.any(Object),
        expect.any(Function)
      );
    });

    it('returns structured play mode state from Unity', async () => {
      registerGetPlayModeStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockResolvedValue({
        success: true,
        type: 'text',
        isPlaying: true,
        isPaused: false
      });

      const result = await handler({});

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'get_play_mode_status',
        params: {}
      });
      expect(result.content[0].text).toBe('Play mode');
      expect(result.data).toEqual({ isPlaying: true, isPaused: false });
    });

    it('throws a tool execution error when Unity reports failure', async () => {
      registerGetPlayModeStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockResolvedValue({
        success: false,
        message: 'Unity unavailable'
      });

      await expect(handler({})).rejects.toMatchObject({
        type: ErrorType.TOOL_EXECUTION,
        message: 'Unity unavailable'
      } as Partial<McpUnityError>);
    });
  });

  describe('set_play_mode_status', () => {
    it('registers the tool with the MCP server', () => {
      registerSetPlayModeStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);

      expect(mockServerTool).toHaveBeenCalledWith(
        'set_play_mode_status',
        expect.any(String),
        expect.objectContaining({ action: expect.any(Object) }),
        expect.any(Function)
      );
    });

    it('sends validated play mode actions to Unity', async () => {
      registerSetPlayModeStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      mockSendRequest.mockResolvedValue({
        success: true,
        isPlaying: false,
        isPaused: false,
        action: 'stop'
      });

      const result = await handler({ action: 'stop' });

      expect(mockSendRequest).toHaveBeenCalledWith({
        method: 'set_play_mode_status',
        params: { action: 'stop' }
      });
      expect(result.content[0].text).toContain("Play mode action 'stop'");
      expect(result.data).toEqual({
        action: 'stop',
        isPlaying: false,
        isPaused: false
      });
    });

    it('rejects invalid actions before calling Unity', async () => {
      registerSetPlayModeStatusTool(mockServer as any, mockMcpUnity as any, mockLogger as any);
      const handler = mockServerTool.mock.calls[0][3] as (params: any) => Promise<any>;

      await expect(handler({ action: 'restart' })).rejects.toThrow();
      expect(mockSendRequest).not.toHaveBeenCalled();
    });
  });
});
