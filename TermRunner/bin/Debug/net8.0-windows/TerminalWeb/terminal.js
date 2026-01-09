(() => {
  const host = window.chrome && window.chrome.webview;
  if (!host) {
    return;
  }

  const container = document.getElementById("terminal");
  const term = new Terminal({
    cursorBlink: true,
    fontSize: 13,
    scrollback: 5000
  });
  const fitAddon = new FitAddon.FitAddon();
  term.loadAddon(fitAddon);
  term.open(container);

  let sessionId = null;
  let resizeTimer = null;

  const darkTheme = {
    background: "#0f0f10",
    foreground: "#e6e6e6",
    cursor: "#ffffff"
  };

  function postMessage(type, payload) {
    host.postMessage({ type, payload });
  }

  function sendReady() {
    postMessage("ready", {
      sessionId,
      cols: term.cols,
      rows: term.rows
    });
  }

  function sendResize() {
    postMessage("resize", {
      sessionId,
      cols: term.cols,
      rows: term.rows
    });
  }

  term.onData((data) => {
    if (!sessionId) {
      return;
    }

    postMessage("input", {
      sessionId,
      text: data
    });
  });

  host.addEventListener("message", (event) => {
    const message = event.data;
    if (!message || !message.type || !message.payload) {
      return;
    }

    if (message.type === "init") {
      sessionId = message.payload.sessionId;
      if (message.payload.theme === "dark") {
        term.setOption("theme", darkTheme);
      }
      if (message.payload.fontSize) {
        term.setOption("fontSize", message.payload.fontSize);
      }
      if (message.payload.scrollbackLines) {
        term.setOption("scrollback", message.payload.scrollbackLines);
      }

      fitAddon.fit();
      sendReady();
      term.focus();
      return;
    }

    if (message.type === "output") {
      if (message.payload.sessionId !== sessionId) {
        return;
      }

      term.write(message.payload.text || "");
    }
  });

  const resizeObserver = new ResizeObserver(() => {
    if (!sessionId) {
      return;
    }

    if (resizeTimer) {
      clearTimeout(resizeTimer);
    }

    resizeTimer = setTimeout(() => {
      fitAddon.fit();
      sendResize();
    }, 50);
  });

  resizeObserver.observe(container);
})();
