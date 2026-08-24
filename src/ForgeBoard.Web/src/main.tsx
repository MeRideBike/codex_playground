import { StrictMode, useCallback, useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { approveMission, createMission, downloadMissionAudit, getMission, listMissionChanges, listMissionTemplates, listMissions, listSamples, retryMission, type ApprovalOutcome, type EngineeringMissionTemplate, type Mission, type RunStep, type SampleDefinition, type SampleFileChange } from "./api";
import "./styles.css";
import "./milestone3.css";

const DEFAULT_TEMPLATE_ID = "diagnose-failing-test";
const DEFAULT_TITLE = "Diagnose and repair the calculator regression";
const DEFAULT_DESCRIPTION = "Find the reason the calculator test fails, apply the reviewed correction in an isolated workspace, and verify the result.";
const AGENTS = [["Planner", "Turns the request into a clear plan."], ["Researcher", "Collects evidence from the sample."], ["Implementer", "Applies only a reviewed correction."], ["Test Engineer", "Records deterministic verification."], ["Reviewer", "Summarizes the final evidence."]];

function App() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [selectedId, setSelectedId] = useState<string>();
  const [selectedStepId, setSelectedStepId] = useState<string>();
  const [samples, setSamples] = useState<SampleDefinition[]>([]);
  const [templates, setTemplates] = useState<EngineeringMissionTemplate[]>([]);
  const [templateId, setTemplateId] = useState(DEFAULT_TEMPLATE_ID);
  const [sampleId, setSampleId] = useState("defective-calculator");
  const [title, setTitle] = useState(DEFAULT_TITLE);
  const [description, setDescription] = useState(DEFAULT_DESCRIPTION);
  const [maxTokenUsage, setMaxTokenUsage] = useState("");
  const [maxEstimatedCost, setMaxEstimatedCost] = useState("");
  const [comment, setComment] = useState("The evidence is clear. Proceed with the reviewed operation.");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>();
  const [changes, setChanges] = useState<SampleFileChange[]>([]);

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
  useEffect(() => { void listSamples().then(setSamples).catch(() => undefined); }, []);
  useEffect(() => { void listMissionTemplates().then(setTemplates).catch(() => undefined); }, []);
  useEffect(() => {
    const connection = new HubConnectionBuilder().withUrl("/hubs/missions").withAutomaticReconnect().configureLogging(LogLevel.Warning).build();
    connection.on("missionUpdated", (missionId: string) => void refresh(missionId));
    void connection.start().catch(() => undefined);
    return () => { void connection.stop(); };
  }, [refresh]);

  const selectedMission = useMemo(() => missions.find((mission) => mission.id === selectedId), [missions, selectedId]);
  const selectedStep = useMemo(() => selectedMission?.steps.find((step) => step.id === selectedStepId) ?? orderSteps(selectedMission?.steps ?? [])[0], [selectedMission, selectedStepId]);
  const selectedTemplate = useMemo(() => templates.find((template) => template.id === templateId), [templates, templateId]);
  const awaitingApproval = selectedMission?.steps.find((step) => step.status === "AwaitingApproval");

  useEffect(() => {
    if (!selectedMission?.id) { setChanges([]); return; }
    void listMissionChanges(selectedMission.id).then(setChanges).catch(() => setChanges([]));
  }, [selectedMission?.id, selectedMission?.status, selectedMission?.completedAt]);

  function chooseTemplate(template: EngineeringMissionTemplate) {
    setTemplateId(template.id); setSampleId(template.sampleId);
    setTitle(templateTitle(template)); setDescription(template.description); setSelectedStepId(undefined);
  }

  async function submitMission(event: React.FormEvent) {
    event.preventDefault();
    if (!title.trim() || !description.trim()) return;
    const tokenLimit = maxTokenUsage.trim() ? Number(maxTokenUsage) : undefined;
    const costLimit = maxEstimatedCost.trim() ? Number(maxEstimatedCost) : undefined;
    if ((tokenLimit !== undefined && (!Number.isInteger(tokenLimit) || tokenLimit <= 0)) || (costLimit !== undefined && (!Number.isFinite(costLimit) || costLimit <= 0))) {
      setError("Budget limits must be positive numbers."); return;
    }
    setSubmitting(true);
    try {
      const mission = await createMission(title.trim(), description.trim(), sampleId, templateId, tokenLimit, costLimit);
      setMissions((current) => [mission, ...current]); setSelectedId(mission.id); setSelectedStepId(undefined); setError(undefined);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Mission creation failed."); }
    finally { setSubmitting(false); }
  }

  async function decide(outcome: ApprovalOutcome) {
    if (!selectedMission || !comment.trim()) return;
    setSubmitting(true);
    try {
      const mission = await approveMission(selectedMission.id, outcome, comment.trim());
      setMissions((current) => current.map((item) => item.id === mission.id ? mission : item)); setSelectedStepId(undefined); setError(undefined);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Approval decision failed."); }
    finally { setSubmitting(false); }
  }

  async function retryFailedMission() {
    if (!selectedMission) return;
    setSubmitting(true);
    try {
      const mission = await retryMission(selectedMission.id);
      setMissions((current) => current.map((item) => item.id === mission.id ? mission : item)); setSelectedStepId(undefined); setError(undefined);
    } catch (reason) { setError(reason instanceof Error ? reason.message : "Retry failed."); }
    finally { setSubmitting(false); }
  }

  return <div className="app-shell">
    <aside className="sidebar">
      <a className="brand" href="#top"><span>F</span> ForgeBoard</a>
      <p className="sidebar-copy">Auditable engineering workflows, with a human decision before change.</p>
      <nav><a className="active" href="#showcase">Start a mission</a><a href="#missions">Mission history</a><a href="#agents">How it works</a></nav>
      <div className="sidebar-note"><span className="status-dot" /> Local deterministic mode<br /><small>Safe demo · no AI account required</small></div>
    </aside>
    <main id="top">
      <header className="topbar"><div><p className="eyebrow">ForgeBoard showcase</p><h1>Take an engineering task from evidence to verified change.</h1><p>Choose a bounded scenario, inspect the plan, then approve the work only when you are satisfied.</p></div><button className="quiet-button" onClick={() => void refresh()} disabled={loading}>Refresh data</button></header>
      {error && <div role="alert" className="error-banner">{error}</div>}

      <section id="showcase" className="showcase-hero" aria-labelledby="showcase-heading">
        <div><p className="eyebrow">Guided walkthrough</p><h2 id="showcase-heading">A complete demo in three clear steps</h2></div>
        <ol className="walkthrough"><li><span>1</span><div><strong>Choose a scenario</strong><small>Each scenario has reviewed evidence and bounded operations.</small></div></li><li><span>2</span><div><strong>Review the plan</strong><small>Planner and Researcher run first, then ForgeBoard pauses.</small></div></li><li><span>3</span><div><strong>Approve or stop</strong><small>Your decision is recorded before any isolated change occurs.</small></div></li></ol>
      </section>

      <section className="mission-composer panel" aria-labelledby="new-mission-heading">
        <div className="composer-heading"><div><p className="eyebrow">Start a mission</p><h2 id="new-mission-heading">Pick a reviewed engineering scenario</h2><p>Use a showcase template for a reproducible end-to-end run, or switch to Custom mission for exploratory planning.</p></div>{selectedTemplate && <div className="template-scope"><span>Bounded scope</span><strong>{selectedTemplate.allowedCapabilities.length} allowed capabilities</strong></div>}</div>
        <div className="template-grid">{templates.map((template, index) => <button key={template.id} type="button" className={`template-card ${template.id === templateId ? "selected" : ""}`} onClick={() => chooseTemplate(template)}><span>{String(index + 1).padStart(2, "0")}</span><strong>{template.displayName}</strong><small>{template.description}</small><em>{template.id === templateId ? "Selected" : "Use scenario"}</em></button>)}</div>
        <form className="mission-form" onSubmit={submitMission}>
          <label><span>Mission title</span><input aria-label="Mission title" value={title} onChange={(event) => setTitle(event.target.value)} maxLength={200} /></label>
          <label><span>Scenario</span><select aria-label="Mission template" value={templateId} onChange={(event) => { const next = event.target.value; setTemplateId(next); const template = templates.find((item) => item.id === next); if (template) chooseTemplate(template); }}><option value="">Custom mission</option>{templates.map((template) => <option key={template.id} value={template.id}>{template.displayName}</option>)}</select></label>
          <label><span>Sample environment</span><select aria-label="Sample environment" value={sampleId} onChange={(event) => setSampleId(event.target.value)} disabled={Boolean(templateId)}><option value="">No sample environment</option>{samples.map((sample) => <option key={sample.id} value={sample.id}>{sample.displayName}</option>)}</select></label>
          <label><span>Token limit <small>optional</small></span><input aria-label="Token budget" type="number" min="1" step="1" value={maxTokenUsage} onChange={(event) => setMaxTokenUsage(event.target.value)} placeholder="No limit" /></label>
          <label><span>Cost limit <small>optional</small></span><input aria-label="Estimated cost budget" type="number" min="0.000001" step="0.000001" value={maxEstimatedCost} onChange={(event) => setMaxEstimatedCost(event.target.value)} placeholder="No limit" /></label>
          <label className="mission-description"><span>What should ForgeBoard accomplish?</span><textarea aria-label="Mission description" value={description} onChange={(event) => setDescription(event.target.value)} maxLength={10000} rows={3} /></label>
          <div className="composer-footer"><p>{selectedTemplate ? "The selected policy is locked to this mission and visible in its audit record." : "Custom missions remain planning-only unless a reviewed sample template is selected."}</p><button className="primary-button" disabled={submitting || !title.trim() || !description.trim()}>{submitting ? "Starting mission…" : "Start guided mission"}</button></div>
        </form>
      </section>

      <section id="missions" className="workbench" aria-label="Mission workbench">
        <div className="mission-list panel"><div className="panel-heading"><div><p className="eyebrow">Mission history</p><h2>Runs you can inspect</h2></div><span className="count">{missions.length}</span></div>{loading && <p className="empty-state">Loading your local missions…</p>}{!loading && missions.length === 0 && <div className="empty-state"><strong>No mission yet.</strong><p>Choose a scenario above and start the guided walkthrough.</p></div>}{missions.map((mission) => <button key={mission.id} className={`mission-card ${mission.id === selectedMission?.id ? "selected" : ""}`} onClick={() => { setSelectedId(mission.id); setSelectedStepId(undefined); }}><div><strong>{mission.title}</strong><small>{mission.templateId ? prettyTemplate(mission.templateId) : "Custom mission"}</small></div><StatusBadge status={mission.status} /><div className="mission-card-footer"><span>{completion(mission)}% complete</span><span>{formatDuration(mission.startedAt, mission.completedAt)}</span></div></button>)}</div>
        <MissionDetail mission={selectedMission} selectedStep={selectedStep} changes={changes} onSelectStep={setSelectedStepId} />
      </section>

      {awaitingApproval && selectedMission && <section className="approval-panel panel" aria-labelledby="approval-heading"><div><p className="eyebrow">Your decision is needed</p><h2 id="approval-heading">Review the evidence before allowing the next stage</h2><p>The plan and research are complete. Approve to release the reviewed operation, ask for stronger research, or stop the mission.</p></div><div className="approval-controls"><textarea aria-label="Approval comment" value={comment} onChange={(event) => setComment(event.target.value)} maxLength={4000} rows={3} /><div><button className="approve" disabled={submitting || !comment.trim()} onClick={() => void decide("Approved")}>Approve reviewed work</button><button className="revision" disabled={submitting || !comment.trim()} onClick={() => void decide("RevisionRequested")}>Request revision</button><button className="reject" disabled={submitting || !comment.trim()} onClick={() => void decide("Rejected")}>Stop mission</button></div></div></section>}
      {selectedMission?.status === "Failed" && <section className="approval-panel panel" aria-label="Failed mission recovery"><div><p className="eyebrow">Workflow recovery</p><h2>Retry the failed step</h2><p>The previous error and observed usage remain in the audit record. Retrying is always an explicit human action.</p></div><div className="approval-controls"><button className="revision" disabled={submitting} onClick={() => void retryFailedMission()}>Retry failed step</button></div></section>}

      <section id="agents" className="agents panel"><div className="panel-heading"><div><p className="eyebrow">How ForgeBoard works</p><h2>Five responsibilities, one observable workflow</h2></div></div><div className="agent-grid">{AGENTS.map(([name, responsibility]) => <article key={name}><span>{name.slice(0, 1)}</span><div><h3>{name}</h3><p>{responsibility}</p></div></article>)}</div></section>
    </main>
  </div>;
}

function MissionDetail({ mission, selectedStep, changes, onSelectStep }: { mission?: Mission; selectedStep?: RunStep; changes: SampleFileChange[]; onSelectStep: (id: string) => void }) {
  if (!mission) return <section className="detail panel empty-detail"><p className="eyebrow">Mission workbench</p><h2>Select a run to see its evidence and approval status.</h2><p>When you start a guided mission, this area becomes your audit trail: what happened, why it happened, and what changed.</p></section>;
  const steps = orderSteps(mission.steps);
  const events = [...steps.map((step) => ({ time: step.completedAt ?? step.startedAt, text: `${step.displayName}: ${labelStatus(step.status)}` })), ...mission.approvals.map((approval) => ({ time: approval.decidedAt, text: `Human decision: ${labelStatus(approval.outcome)}` }))].sort((left, right) => (right.time ?? "").localeCompare(left.time ?? ""));
  return <section className="detail panel"><div className="detail-header"><div><p className="eyebrow">Mission workbench</p><h2>{mission.title}</h2><p>{mission.description}</p></div><div className="detail-actions"><StatusBadge status={mission.status} /><button className="quiet-button" onClick={() => void downloadMissionAudit(mission.id)}>Export audit</button></div></div>
    <div className="mission-facts"><Fact label="Scenario" value={mission.templateId ? prettyTemplate(mission.templateId) : "Custom mission"} /><Fact label="Sample" value={mission.sampleId ?? "None"} /><Fact label="Progress" value={`${completion(mission)}% complete`} /><Fact label="Budget" value={formatBudget(mission)} /></div>
    <div className="workflow"><div className="section-title"><div><p className="eyebrow">Workflow timeline</p><h3>Follow the run from plan to review</h3></div><span>{nextAction(mission)}</span></div>{steps.map((step, index) => <button key={step.id} className={`step ${step.id === selectedStep?.id ? "selected" : ""}`} onClick={() => onSelectStep(step.id)}><span className={`step-marker ${step.status}`}>{statusSymbol(step.status)}</span><span><strong>{step.displayName}</strong><small>{labelStatus(step.status)}{step.startedAt && ` · ${formatDuration(step.startedAt, step.completedAt)}`}</small></span>{index < steps.length - 1 && <i />}</button>)}</div>
    <div className="step-inspector">{selectedStep ? <><div className="panel-heading"><div><p className="eyebrow">Selected evidence</p><h3>{selectedStep.displayName}</h3><p>{plainSummary(selectedStep)}</p></div><StatusBadge status={selectedStep.status} /></div><div className="metadata"><Fact label="Provider" value={selectedStep.provider ?? "Not run"} /><Fact label="Model" value={selectedStep.model ?? "—"} /><Fact label="Usage" value={`${selectedStep.tokenUsage ?? 0} tokens · $${selectedStep.estimatedCost ?? 0}`} /><Fact label="Retries" value={String(selectedStep.retryAttempts)} /></div><AuditDetails label="Input sent to this stage" value={selectedStep.input} /><AuditDetails label="Structured result" value={selectedStep.structuredOutput} /><AuditDetails label="Tools and evidence" value={selectedStep.toolResults} /><AuditDetails label="Operation policy" value={mission.operationPolicy} /><AuditDetails label="Rationale summary" value={selectedStep.rationaleSummary} />{selectedStep.error && <AuditDetails label={selectedStep.status === "Succeeded" ? "Prior error" : "Error"} value={selectedStep.error} open />}</> : <p className="empty-state">Select a workflow stage to inspect its evidence.</p>}</div>
    <SourceChanges changes={changes} />
    <div className="audit-timeline"><p className="eyebrow">Chronological audit</p>{events.length ? events.map((event, index) => <p key={`${event.text}-${index}`}><time>{formatTimestamp(event.time)}</time>{event.text}</p>) : <p className="empty-state">No events recorded.</p>}</div>
  </section>;
}

function SourceChanges({ changes }: { changes: SampleFileChange[] }) {
  if (changes.length === 0) return null;
  return <section className="source-changes" aria-label="Reviewed source changes"><div className="section-title"><div><p className="eyebrow">Reviewed source change</p><h3>Exactly what changed in the isolated sample</h3></div><span>{changes.length} file{changes.length === 1 ? "" : "s"}</span></div>{changes.map((change) => <article key={change.path}><code>{change.path}</code><div className="diff-columns"><div><small>Before</small><pre>{change.originalContent}</pre></div><div><small>After</small><pre>{change.updatedContent}</pre></div></div></article>)}</section>;
}

function Fact({ label, value }: { label: string; value: string }) { return <span><small>{label}</small><strong>{value}</strong></span>; }
function AuditDetails({ label, value, open = false }: { label: string; value?: string | null; open?: boolean }) { if (!value) return null; let text = value; try { text = JSON.stringify(JSON.parse(value), null, 2); } catch { /* Audit summaries can be plain text. */ } return <details className="audit-details" open={open}><summary>{label}</summary><pre>{text}</pre></details>; }
function StatusBadge({ status }: { status: string }) { return <span className={`badge ${status}`}>{labelStatus(status)}</span>; }
function templateTitle(template: EngineeringMissionTemplate) { return template.displayName === "Diagnose failing test" ? DEFAULT_TITLE : template.displayName; }
function prettyTemplate(value: string) { return value.replace(/-/g, " ").replace(/\b\w/g, (letter) => letter.toUpperCase()); }
function labelStatus(status: string) { return status.replace(/([a-z])([A-Z])/g, "$1 $2"); }
function statusSymbol(status: string) { return status === "Succeeded" || status === "Completed" ? "✓" : status === "AwaitingApproval" ? "!" : status === "Failed" || status === "Rejected" ? "×" : status === "Running" ? "·" : "○"; }
function orderSteps(steps: RunStep[]) { const order: Record<string, number> = { planner: 0, researcher: 1, approval: 2, implementer: 3, "test-engineer": 4, reviewer: 5 }; return [...steps].sort((left, right) => workflowPosition(left, order) - workflowPosition(right, order)); }
function workflowPosition(step: RunStep, order: Record<string, number>) { if (step.nodeKey.startsWith("researcher-revision-")) return 2.1; if (step.nodeKey.startsWith("approval-revision-")) return 2.2; return order[step.nodeKey] ?? Number.MAX_SAFE_INTEGER; }
function completion(mission: Mission) { return Math.round(mission.steps.filter((step) => ["Succeeded", "Rejected", "Failed"].includes(step.status)).length / mission.steps.length * 100); }
function formatDuration(start?: string | null, end?: string | null) { if (!start) return "Not started"; const milliseconds = (end ? new Date(end) : new Date()).getTime() - new Date(start).getTime(); const seconds = Math.max(0, Math.round(milliseconds / 1000)); return seconds < 60 ? `${seconds}s` : `${Math.floor(seconds / 60)}m ${seconds % 60}s`; }
function formatTimestamp(value?: string | null) { return value ? new Date(value).toLocaleString() : "Pending"; }
function formatBudget(mission: Mission) { if (!mission.maxTokenUsage && !mission.maxEstimatedCost) return "No limits"; return `${mission.maxTokenUsage ? `${mission.maxTokenUsage} tokens` : "—"} · ${mission.maxEstimatedCost ? `$${mission.maxEstimatedCost}` : "—"}`; }
function nextAction(mission: Mission) { if (mission.status === "AwaitingApproval") return "Your approval is required"; if (mission.status === "Failed") return "A retry is available"; if (mission.status === "Completed") return "Completed and auditable"; return "Workflow in progress"; }
function plainSummary(step: RunStep) { if (step.status === "AwaitingApproval") return "The workflow is paused until a person makes a recorded decision."; if (step.status === "Pending") return "This stage starts only after the earlier evidence is complete."; if (step.status === "Failed") return "Inspect the error and retry only if the earlier evidence still supports it."; return "Open the evidence sections below to see the exact structured record."; }

createRoot(document.getElementById("root")!).render(<StrictMode><App /></StrictMode>);
