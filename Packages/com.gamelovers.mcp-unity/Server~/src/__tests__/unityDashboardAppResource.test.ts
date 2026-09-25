import { jest, describe, it, expect, beforeEach } from '@jest/globals';
import { RESOURCE_MIME_TYPE } from '@modelcontextprotocol/ext-apps/server';
import { registerShowUnityDashboardTool } from '../tools/showUnityDashboardTool.js';
import {
  readUnityDashboardHtml,
  registerUnityDashboardAppResource
} from '../resources/unityDashboardAppResource.js';

const mockLogger = {
  info: jest.fn(),
  debug: jest.fn(),
  warn: jest.fn(),
  error: jest.fn()
};

const mockRegisterTool = jest.fn();
const mockRegisterResource = jest.fn();
const mockResource = jest.fn();

describe('Unity Dashboard MCP App', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('loads the bundled dashboard HTML from a module-relative path', () => {
    const result = readUnityDashboardHtml();

    expect(result.mimeType).toBe(RESOURCE_MIME_TYPE);
    expect(result.text).toContain('<title>Unity Dashboard</title>');
    expect(result.text).toContain('set_play_mode_status');
  });

  it('registers app and legacy dashboard resources', async () => {
    registerUnityDashboardAppResource({
      registerResource: mockRegisterResource,
      resource: mockResource
    } as any, mockLogger as any);

    expect(mockRegisterResource).toHaveBeenCalledWith(
      'unity_dashboard_app',
      'ui://unity-dashboard',
      expect.objectContaining({
        description: expect.stringContaining('Unity dashboard')
      }),
      expect.any(Function)
    );
    expect(mockResource).toHaveBeenCalledWith(
      'unity_dashboard_app_legacy',
      'unity://ui/dashboard',
      expect.objectContaining({
        mimeType: RESOURCE_MIME_TYPE
      }),
      expect.any(Function)
    );

    const appRead = mockRegisterResource.mock.calls[0][3] as () => Promise<any>;
    const legacyRead = mockResource.mock.calls[0][3] as () => Promise<any>;

    await expect(appRead()).resolves.toMatchObject({
      contents: [
        expect.objectContaining({
          uri: 'ui://unity-dashboard',
          mimeType: RESOURCE_MIME_TYPE
        })
      ]
    });
    await expect(legacyRead()).resolves.toMatchObject({
      contents: [
        expect.objectContaining({
          uri: 'unity://ui/dashboard',
          mimeType: RESOURCE_MIME_TYPE
        })
      ]
    });
  });

  it('registers the show dashboard app tool with normalized UI metadata', async () => {
    registerShowUnityDashboardTool({
      registerTool: mockRegisterTool
    } as any, mockLogger as any);

    expect(mockRegisterTool).toHaveBeenCalledWith(
      'show_unity_dashboard',
      expect.objectContaining({
        description: expect.stringContaining('Unity dashboard'),
        _meta: expect.objectContaining({
          ui: expect.objectContaining({ resourceUri: 'ui://unity-dashboard' }),
          'ui/resourceUri': 'ui://unity-dashboard'
        })
      }),
      expect.any(Function)
    );

    const handler = mockRegisterTool.mock.calls[0][2] as () => Promise<any>;
    const result = await handler();

    expect(result.content[0]).toMatchObject({
      type: 'resource',
      resource: {
        uri: 'ui://unity-dashboard',
        mimeType: RESOURCE_MIME_TYPE
      }
    });
    expect(result._meta.ui).toEqual({
      resourceUri: 'ui://unity-dashboard',
      title: 'Unity Dashboard'
    });
  });
});
