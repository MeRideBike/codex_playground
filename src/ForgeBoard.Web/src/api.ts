export type ApprovalOutcome = "Approved" | "Rejected" | "RevisionRequested";
export interface RunStep { id: string; nodeKey: string; displayName: string; kind: string; status: string; startedAt?: string | null; completedAt?: string | null; input?: string | null; structuredOutput?: string | null; rationaleSummary?: string | null; provider?: string | null; model?: string | null; toolsInvoked?: string | null; toolResults?: string | null; error?: string | null; retryAttempts: number; tokenUsage?: number | null; estimatedCost?: number | null; }
export interface ApprovalRecord { id: string; stepId: string; outcome: string; comment: string; decidedAt: string; }
export interface Mission { id: string; title: string; description: string; workflowName: string; status: string; createdAt: string; startedAt?: string | null; completedAt?: string | null; steps: RunStep[]; approvals: ApprovalRecord[]; }
async function request<T>(path: string, options?: RequestInit): Promise<T> { const response = await fetch(path, { ...options, headers: { "Content-Type": "application/json", ...options?.headers } }); if (!response.ok) throw new Error((await response.text()) || `Request failed (${response.status}).`); return response.json() as Promise<T>; }
export const listMissions = () => request<Mission[]>("/api/missions");
export const getMission = (id: string) => request<Mission>(`/api/missions/${id}`);
export const createMission = (title: string, description: string) => request<Mission>("/api/missions", { method: "POST", body: JSON.stringify({ title, description }) });
export const approveMission = (id: string, outcome: ApprovalOutcome, comment: string) => request<Mission>(`/api/missions/${id}/approval`, { method: "POST", body: JSON.stringify({ outcome, comment }) });
