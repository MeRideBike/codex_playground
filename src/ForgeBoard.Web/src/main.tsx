import { StrictMode, useCallback, useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { approveMission, createMission, getMission, listMissions, type ApprovalOutcome, type Mission, type RunStep } from "./api";
import "./styles.css";

const AGENTS = [["Planner", "Turns the mission into a clear, inspectable plan."], ["Researcher", "Collects structured evidence and recommendations."], ["Implementer", "Proposes the smallest auditable correction."], ["Test Engineer", "Records verification evidence and limitations."], ["Reviewer", "Assesses the resulting change and its evidence."]];

function App() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [selectedId, setSelectedId] = useState<string>();
  const [selectedStepId, setSelectedStepId] = useState<string>();
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [comment, setComment] = useState("Approved after reviewing the research artifacts.");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  const refresh = useCallback(async (missionId?: string) => {
    try {
      const data = missionId ? [await getMission(missionId)] : await listMissions();
      setMissions((current) => missionId ? current.map((mission) => mission.id === missionId ? data[0] : mission) : data);
      setSelectedId((current) => current ?? data[0]?.id);
      setError(undefined);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "ForgeBoard could not reach the API."); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => { void refresh(); }, [refresh]);
  useEffect(() => {
    const connection = new HubConnectionBuilder().withUrl("/hubs/missions").withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
    connection.on("missionUpdated", (missionId: string) => void refresh(missionId));
    void connection.start().catch(() => undefined);
    return () => { void connection.stop(); };
  }, [refresh]);

  const selectedMission = useMemo(() => missions.find((mission) => mission.id === selectedId), [missions, selectedId]);
  const selectedStep = useMemo(() => selectedMission?.steps.find((step) => step.id === selectedStepId) ?? selectedMission?.steps[0], [selectedMission, selectedStepId]);
  const awaitingApproval = selectedMission?.steps.find((step) => step.status === "AwaitingApproval");

  async function submitMission(event: React.FormEvent) {
    event.preventDefault(); if (!title.trim() || !description.trim()) return;
    setSubmitting(true);
    try { const mission = await createMission(title.trim(), description.trim()); setMissions((current) => [mission, ...current]); setSelectedId(mission.id); setSelectedStepId(undefined); setTitle(""); setDescription(""); setError(undefined); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Mission creation failed."); }
    finally { setSubmitting(false); }
  }
  async function decide(outcome: ApprovalOutcome) {
    if (!selectedMission || !comment.trim()) return;
    setSubmitting(true);
    try { const mission = await approveMission(selectedMission.id, outcome, comment.trim()); setMissions((current) => current.map((item) => item.id === mission.id ? mission : item)); setSelectedStepId(undefined); setError(undefined); }
    catch (reason) { setError(reason instanceof Error ? reason.message : "Approval decision failed."); }
    finally { setSubmitting(false); }
  }

  return <div className="app-shell">
    <aside className="sidebar"><a className="brand" href="#top"><span>F</span> ForgeBoard</a><p className="eyebrow">Engineering orchestration</p><nav><a className="active" href="#missions">Missions</a><a href="#agents">Agent definitions</a></nav><div className="sidebar-note"><span className="status-dot" /> Deterministic provider<br /><small>No external credentials configured</small></div></aside>
    <main id="top"><header className="topbar"><div><p className="eyebrow">Mission control</p><h1>Audit-first engineering work</h1></div><button className="quiet-button" onClick={() => void refresh()} disabled={loading}>Refresh</button></header>
      {error && <div role="alert" className="error-banner">{error}</div>}
      <section className="new-mission" aria-labelledby="new-mission-heading"><div><p className="eyebrow">New mission</p><h2 id="new-mission-heading">Start a controlled workflow</h2></div><form onSubmit={submitMission}><input aria-label="Mission title" value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Diagnose the failing calculator test" maxLength={200} /><textarea aria-label="Mission description" value={description} onChange={(event) => setDescription(event.target.value)} placeholder="State the engineering objective and evidence to examine." maxLength={10000} rows={2} /><button disabled={submitting || !title.trim() || !description.trim()}>{submitting ? "Starting…" : "Start mission"}</button></form></section>
      <section id="missions" className="dashboard-grid" aria-label="Mission dashboard"><div className="mission-list panel"><div className="panel-heading"><div><p className="eyebrow">Missions</p><h2>Current runs</h2></div><span className="count">{missions.length}</span></div>{loading && <p className="empty-state">Loading missions…</p>}{!loading && missions.length === 0 && <p className="empty-state">No missions yet. Start one to create an auditable run.</p>}{missions.map((mission) => <button key={mission.id} className={`mission-card ${mission.id === selectedMission?.id ? "selected" : ""}`} onClick={() => { setSelectedId(mission.id); setSelectedStepId(undefined); }}><div><strong>{mission.title}</strong><small>{mission.workflowName}</small></div><StatusBadge status={mission.status} /><div className="mission-card-footer"><span>{completion(mission)}% complete</span><span>{formatDuration(mission.startedAt, mission.completedAt)}</span></div></button>)}</div><MissionDetail mission={selectedMission} selectedStep={selectedStep} onSelectStep={setSelectedStepId} /></section>
      {awaitingApproval && selectedMission && <section className="approval-panel panel" aria-labelledby="approval-heading"><div><p className="eyebrow">Human approval required</p><h2 id="approval-heading">{awaitingApproval.displayName}</h2><p>The workflow is paused. Your decision and comment become part of the mission audit trail.</p></div><div className="approval-controls"><textarea aria-label="Approval comment" value={comment} onChange={(event) => setComment(event.target.value)} maxLength={4000} rows={3} /><div><button className="approve" disabled={submitting || !comment.trim()} onClick={() => void decide("Approved")}>Approve</button><button className="revision" disabled={submitting || !comment.trim()} onClick={() => void decide("RevisionRequested")}>Request revision</button><button className="reject" disabled={submitting || !comment.trim()} onClick={() => void decide("Rejected")}>Reject</button></div></div></section>}
      <section id="agents" className="agents panel"><div className="panel-heading"><div><p className="eyebrow">Agent definitions</p><h2>Configured responsibilities</h2></div></div><div className="agent-grid">{AGENTS.map(([name, responsibility]) => <article key={name}><span>{name.slice(0, 1)}</span><div><h3>{name}</h3><p>{responsibility}</p></div></article>)}</div></section>
    </main></div>;
}

function MissionDetail({ mission, selectedStep, onSelectStep }: { mission?: Mission; selectedStep?: RunStep; onSelectStep: (id: string) => void }) {
  if (!mission) return <section className="detail panel"><p className="empty-state">Select a mission to inspect its workflow, evidence, and audit trail.</p></section>;
  const steps = orderSteps(mission.steps);
  const events = [...steps.map((step) => ({ time: step.completedAt ?? step.startedAt, text: `${step.displayName}: ${labelStatus(step.status)}` })), ...mission.approvals.map((approval) => ({ time: approval.decidedAt, text: `Human decision: ${approval.outcome}` }))].sort((left, right) => (right.time ?? "").localeCompare(left.time ?? ""));
  return <section className="detail panel"><div className="detail-header"><div><p className="eyebrow">Mission detail</p><h2>{mission.title}</h2><p>{mission.description}</p></div><StatusBadge status={mission.status} /></div><div className="workflow"><h3>Execution timeline</h3>{steps.map((step, index) => <button key={step.id} className={`step ${step.id === selectedStep?.id ? "selected" : ""}`} onClick={() => onSelectStep(step.id)}><span className={`step-marker ${step.status}`}>{statusSymbol(step.status)}</span><span><strong>{step.displayName}</strong><small>{labelStatus(step.status)}{step.startedAt && ` · ${formatDuration(step.startedAt, step.completedAt)}`}</small></span>{index < steps.length - 1 && <i />}</button>)}</div>{selectedStep && <div className="step-inspector"><div className="panel-heading"><div><p className="eyebrow">Selected step</p><h3>{selectedStep.displayName}</h3></div><StatusBadge status={selectedStep.status} /></div><AuditField label="Input" value={selectedStep.input} /><AuditField label="Structured output" value={selectedStep.structuredOutput} /><AuditField label="Decision rationale" value={selectedStep.rationaleSummary} /><div className="metadata"><span>Provider <strong>{selectedStep.provider ?? "—"}</strong></span><span>Model <strong>{selectedStep.model ?? "—"}</strong></span><span>Tokens <strong>{selectedStep.tokenUsage ?? "—"}</strong></span><span>Est. cost <strong>{selectedStep.estimatedCost ?? "—"}</strong></span></div>{selectedStep.error && <AuditField label="Error" value={selectedStep.error} />}</div>}<div className="audit-timeline"><p className="eyebrow">Audit timeline</p>{events.length ? events.map((event, index) => <p key={`${event.text}-${index}`}><time>{formatTimestamp(event.time)}</time>{event.text}</p>) : <p className="empty-state">No events recorded.</p>}</div></section>;
}
function AuditField({ label, value }: { label: string; value?: string | null }) { if (!value) return null; let text = value; try { text = JSON.stringify(JSON.parse(value), null, 2); } catch { /* Audit summaries are plain text. */ } return <div className="audit-field"><p>{label}</p><pre>{text}</pre></div>; }
function StatusBadge({ status }: { status: string }) { return <span className={`badge ${status}`}>{labelStatus(status)}</span>; }
function labelStatus(status: string) { return status.replace(/([a-z])([A-Z])/g, "$1 $2"); }
function statusSymbol(status: string) { return status === "Succeeded" || status === "Completed" ? "✓" : status === "AwaitingApproval" ? "!" : status === "Failed" || status === "Rejected" ? "×" : status === "Running" ? "·" : "○"; }
function orderSteps(steps: RunStep[]) { const order: Record<string, number> = { planner: 0, researcher: 1, approval: 2, implementer: 3, "test-engineer": 4, reviewer: 5 }; return [...steps].sort((left, right) => workflowPosition(left, order) - workflowPosition(right, order)); }
function workflowPosition(step: RunStep, order: Record<string, number>) { if (step.nodeKey.startsWith("researcher-revision-")) return 2.1; if (step.nodeKey.startsWith("approval-revision-")) return 2.2; return order[step.nodeKey] ?? Number.MAX_SAFE_INTEGER; }
function completion(mission: Mission) { return Math.round(mission.steps.filter((step) => ["Succeeded", "Rejected", "Failed"].includes(step.status)).length / mission.steps.length * 100); }
function formatDuration(start?: string | null, end?: string | null) { if (!start) return "Not started"; const milliseconds = (end ? new Date(end) : new Date()).getTime() - new Date(start).getTime(); const seconds = Math.max(0, Math.round(milliseconds / 1000)); return seconds < 60 ? `${seconds}s` : `${Math.floor(seconds / 60)}m ${seconds % 60}s`; }
function formatTimestamp(value?: string | null) { return value ? new Date(value).toLocaleString() : "Pending"; }
createRoot(document.getElementById("root")!).render(<StrictMode><App /></StrictMode>);
