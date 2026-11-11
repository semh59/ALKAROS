# V1-RMD-963 Result

Status: Done.

- The header shows the label "Terminal" with the value "D60B3DF9" instead of "Terminal Terminal D60B3DF9".
- Tests: workspace.test 40/40; 4 mutants killed.
- Open product decision: terminals have no name field. Giving them names (set by a manager) would replace the short id here, on the cashier screen (Cashier.tsx prints "Terminal · id" too) and in the manager's terminals panel.
