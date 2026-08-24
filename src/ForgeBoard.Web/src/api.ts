export type ApprovalOutcome = "Approved" | "Rejected" | "RevisionRequested";
export interface RunStep { id: string; nodeKey: string; displayName: string; kind: string; status: string; startedAt?: string | null; completedAt?: string | null; input?: string | null; structuredOutput?: string | null; rationaleSummary?: string | null; provider?: string | null; model?: string | null; toolsInvoked?: string | null; toolResults?: string | null; error?: string | null; retryAttempts: number; tokenUsage?: number | null; estimatedCost?: number | null; }
export interface ApprovalRecord { id: string; stepId: string; outcome: string; comment: string; decidedAt: string; }
export interface SampleDefinition { id: string; displayName: string; description: string; }
export interface EngineeringMissionTemplate { id: string; displayName: string; description: string; sampleId: string; allowedCapabilities: string[]; }
export interface SampleFileChange { path: string; originalContent: string; updatedContent: string; }
export interface Mission { id: string; title: string; description: string; workflowName: string; sampleId?: string | null; templateId?: string | null; maxTokenUsage?: number | null; maxEstimatedCost?: number | null; operationPolicy: string; status: string; createdAt: string; startedAt?: string | null; completedAt?: string | null; steps: RunStep[]; approvals: ApprovalRecord[]; }
async function request<T>(path: string, options?: RequestInit): Promise<T> { const response = await fetch(path, { ...options, headers: { "Content-Type": "application/json", ...options?.headers } }); if (!response.ok) throw new Error((await response.text()) || `Request failed (${response.status}).`); return response.json() as Promise<T>; }
export const listMissions = () => request<Mission[]>("/api/missions");
export const getMission = (id: string) => request<Mission>(`/api/missions/${id}`);
export const listMissionChanges = (id: string) => request<SampleFileChange[]>(`/api/missions/${id}/changes`);
export const listSamples = () => request<SampleDefinition[]>("/api/samples");
export const listMissionTemplates = () => request<EngineeringMissionTemplate[]>("/api/mission-templates");
export const createMission = (title: string, description: string, sampleId?: string, templateId?: string, maxTokenUsage?: number, maxEstimatedCost?: number) => request<Mission>("/api/missions", { method: "POST", body: JSON.stringify({ title, description, sampleId: sampleId || null, templateId: templateId || null, maxTokenUsage: maxTokenUsage ?? null, maxEstimatedCost: maxEstimatedCost ?? null }) });
export const approveMission = (id: string, outcome: ApprovalOutcome, comment: string) => request<Mission>(`/api/missions/${id}/approval`, { method: "POST", body: JSON.stringify({ outcome, comment }) });
export const retryMission = (id: string) => request<Mission>(`/api/missions/${id}/retry`, { method: "POST" });
export const commitIsolatedChange = (id: string, summary: string) => request<Mission>(`/api/missions/${id}/isolated-commit`, { method: "POST", body: JSON.stringify({ summary }) });
export async function downloadMissionAudit(id: string) {
  const response = await fetch(`/api/missions/${id}/audit-export`);
  if (!response.ok) throw new Error((await response.text()) || `Audit export failed (${response.status}).`);
  const link = document.createElement("a");
  link.href = URL.createObjectURL(await response.blob());
  link.download = response.headers.get("content-disposition")?.match(/filename="?([^";]+)"?/)?.[1] ?? `forgeboard-audit-${id}.json`;
  document.body.appendChild(link); link.click(); link.remove(); URL.revokeObjectURL(link.href);
}
