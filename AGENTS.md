CHANGE LOG
- 2025-12-29 | Request: Renumber versioning policy section | Updated reference to 9.5.
- 2025-12-29 | Request: Add versioning policy reference | Added section 9.5 referencing docs/versioning.md.

AGENTS.md  
Autonomous Agent Operating Agreement  
PromptLoom Project

INDEX

1. Purpose and Authority  
1.1 Scope of This Agreement  
1.2 Binding Nature  
1.3 Interpretation and Conflict Resolution  

2. Definitions  

3. Scope of Agent Authority  
3.1 Repository Boundaries  
3.2 Permitted Areas of Operation  
3.3 Explicitly Restricted Assets  

4. Change Classification and PR Requirements  
4.1 Significant Changes  
4.2 Non-Significant Changes  
4.3 Prohibited Silent Changes  

5. Command Execution Rules  
5.1 General Command Permissions  
5.2 Inspection and Analysis Commands  
5.3 Prohibited and Restricted Commands  

6. System-Level Operations  
6.1 Definition of System-Level Actions  
6.2 Mandatory Permission Requirement  

7. Branching and Pull Request Policy  
7.1 Branch Structure  
7.2 Pull Request Targets  
7.3 Merge Authority  

8. Build, Test, and Validation Requirements  
8.1 Mandatory Build and Test Execution  
8.2 Handling Pre-Existing Failures  
8.3 Failure Escalation Rules  

9. Documentation and Code Quality Requirements  
9.1 Public API Documentation  
9.2 File-Level Change Logs  
9.3 Code Style Normalization  
9.5 Versioning Policy  
9.4 Verbosity, Feedback

10. Dependencies and Tooling  
10.1 Dependency Modifications  
10.2 Tooling and SDK Constraints  

11. Tests and Test Scope Expansion  
11.1 Test Creation Authority  
11.2 Scope Expansion Rules  

12. Time and Effort Limits  
12.1 Ten-Minute Rule  
12.2 Escalation Thresholds  

13. Conflict Resolution and Ambiguity Handling  

14. Pre-PR Submission Check-In (Mandatory)  

15. Enforcement and Compliance  

---

1. PURPOSE AND AUTHORITY

1.1 Scope of This Agreement  
This document defines the operational authority, constraints, responsibilities, and expectations governing all autonomous or semi-autonomous agents acting within this repository.

1.2 Binding Nature  
All agents operating on this repository shall comply with the rules defined herein. Deviation is permitted only where explicitly allowed by this agreement.

1.3 Interpretation and Conflict Resolution  
Where ambiguity exists, agents shall resolve pragmatically, document the decision, and surface the rationale before pull request submission.

2. DEFINITIONS

Agent: Any automated or semi-automated system acting on the repository.  
Significant Change: Any modification affecting process flow, program logic, or user-visible appearance.  
System-Level Action: Any action affecting the host system outside the repository.  
PR: Pull Request.  
dev branch: The long-lived integration branch.  
main branch: Release-only branch.

3. SCOPE OF AGENT AUTHORITY

3.1 Repository Boundaries  
All files and folders within the solution are considered fair operating territory unless explicitly restricted.

3.2 Permitted Areas of Operation  
Agents may freely edit solution code, refactor within scope, modify UI, logic, and structure, create new files and folders, and normalize code style in edited files.

3.3 Explicitly Restricted Assets  
Agents shall not modify AGENTS.md, prior release artifacts, or image assets unless explicitly instructed.

4. CHANGE CLASSIFICATION AND PR REQUIREMENTS

4.1 Significant Changes  
Any change that alters process flow, program logic, or application appearance shall require a Pull Request.

4.2 Non-Significant Changes  
Trivial changes that do not alter behavior or output may be included opportunistically only when part of an existing PR.

4.3 Prohibited Silent Changes  
No agent shall introduce behavior, logic, or UI changes without PR visibility.

5. COMMAND EXECUTION RULES

5.1 General Command Permissions  
Agents may freely execute non-destructive commands necessary for development.

5.2 Inspection and Analysis Commands  
Agents may run read-only inspection commands, Python scripts for analysis or inspection, and create temporary analysis artifacts within the repo, provided such artifacts are removed prior to PR submission.

5.3 Prohibited and Restricted Commands  
Commands that delete files, rewrite git history, or alter system state require explicit permission.

6. SYSTEM-LEVEL OPERATIONS

6.1 Definition of System-Level Actions  
System-level actions include software installation, environment variable modification, PATH changes, registry edits, firewall or antivirus changes, driver changes, writing outside the repository, and process termination.

6.2 Mandatory Permission Requirement  
Agents shall not perform system-level actions without explicit approval.

7. BRANCHING AND PULL REQUEST POLICY

7.1 Branch Structure  
The dev branch is the long-lived integration branch. All work shall occur on short-lived task branches.

7.2 Pull Request Targets  
All PRs target dev. If dev does not exist, the agent shall pause and request guidance.

7.3 Merge Authority  
Agents shall never merge PRs under any circumstance.

8. BUILD, TEST, AND VALIDATION REQUIREMENTS

8.1 Mandatory Build and Test Execution  
Before PR submission, agents shall run a full build and run tests where available.

8.2 Handling Pre-Existing Failures  
If failures are trivial, agents shall fix them. If not, agents shall proceed and escalate before submission.

8.3 Failure Escalation Rules  
Unresolved failures require a Draft PR and explicit guidance request.

9. DOCUMENTATION AND CODE QUALITY REQUIREMENTS

9.1 Public API Documentation  
All public methods in edited files must be documented. Undocumented public APIs encountered outside scope shall be logged as TODOs.

9.2 File-Level Change Logs  
Each edited file shall contain a rolling change log at the very top, containing date, issue or request, and solution, retaining entries for the three most recent modification dates.

9.3 Code Style Normalization  
Agents shall normalize style, formatting, and patterns in files they edit.

9.5 Versioning Policy  
All versioning rules are defined in docs/versioning.md and must be followed for version updates, branch naming, and build metadata.

9.4 Verbosity, Feedback
Agents shall frequently provide feedback to the user on how it is progressing, what challeneges it is facing, and what it is working on. The user shall never be left in the dark for more than a couple minutes as to what is happening.


10. DEPENDENCIES AND TOOLING

10.1 Dependency Modifications  
Only directly related dependencies may be updated. If a dependency change causes issues, it shall be reverted and noted.

10.2 Tooling and SDK Constraints  
Agents shall not modify SDK versions, analyzers, or tooling without permission.

11. TESTS AND TEST SCOPE EXPANSION

11.1 Test Creation Authority  
Agents are encouraged to create tests when modifying logic.

11.2 Scope Expansion Rules  
If tests require significant additional code or cross-file changes, the agent shall request approval before PR submission.

12. TIME AND EFFORT LIMITS

12.1 Ten-Minute Rule  
Agents shall not spend more than ten minutes per created bug attempting resolution before escalation.

12.2 Escalation Thresholds  
Multiple issues may share a single time window. Unresolved issues may proceed with disclosure.

13. CONFLICT RESOLUTION AND AMBIGUITY HANDLING

Where this agreement conflicts with project reality, agents shall resolve pragmatically, document the decision, and surface the explanation before PR submission.

14. PRE-PR SUBMISSION CHECK-IN (MANDATORY)

Before submitting or updating any PR, agents shall include the standardized Pre-PR Submission Check-In.

15. ENFORCEMENT AND COMPLIANCE

Failure to comply with this agreement constitutes a violation of operational expectations. Repeated or material violations may result in restriction or revocation of agent autonomy.
