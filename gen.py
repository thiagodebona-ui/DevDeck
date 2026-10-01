# Every word the app says, in every language it says it in - and the generator that turns this
# table into the two C# files the app actually reads.
#
#   python3 gen.py
#
# writes src/DevDeck.Core/Settings/Strings.cs (the table itself) and
# src/DevDeck.App/Localisation/Tr.cs (one property per key, so a view binds to a plain path).
# Both are generated: edit this file, never those.
#
# The table is the source of truth because three columns side by side is the only arrangement in
# which a missing or wrong translation is visible at a glance. The same fact spread across two
# generated dictionaries ten files apart is not reviewable, and the property list in Tr.cs is
# derived from it rather than maintained beside it for the same reason.
#
# Adding a string: put a row here, run this, then use it - {loc:T TheKey} in a view, or
# Strings.Text("TheKey") / Strings.Format("TheKey", value) in code. The check below fails the run
# if a key is asked for anywhere in src/ and is not in this table, and warns about a key here that
# nothing asks for.
#
# Holes are numbered - {0}, {1} - rather than interpolated, so a translation is free to reorder
# them; Portuguese frequently needs to. LanguageTests walks the whole table and checks that both
# columns carry the same set of holes, which is the difference between a typo here and a
# FormatException in front of a user.

T = [
 # --- Automation, at runtime -------------------------------------------------
 ("AutoNoSteps", "No steps yet.", "Nenhum passo ainda."),
 ("AutoNotRunYet", "Not run yet.", "Ainda não executado."),
 ("AutoNoFolder", "No folder to watch.", "Nenhuma pasta para monitorar."),
 ("AutoNoCommand", "No command to run.", "Nenhum comando para executar."),
 ("AutoWatching", "Watching {0} in {1}", "Monitorando {0} em {1}"),
 ("AutoCannotWatch", "Cannot watch that folder: {0}", "Não foi possível monitorar essa pasta: {0}"),
 ("AutoChooseWorkspace", "Choose a workspace on the Commands page first.",
  "Escolha primeiro uma área de trabalho na página Comandos."),
 ("AutoDeckPresent", "This project ships a deck. Import brings its commands in - nothing runs until you run it.",
  "Este projeto traz um deck. Importar traz os comandos dele - nada executa até você executar."),
 ("AutoNoSuchCommand", "No command called \"{0}\".", "Não existe comando chamado \"{0}\"."),
 ("AutoNoSuchCommands", "No command called {0}.", "Não existe comando chamado {0}."),
 ("AutoNewChain", "New chain", "Nova sequência"),
 ("AutoNotRunNoCommand", "Not run - no command called \"{0}\".",
  "Não executado - não existe comando chamado \"{0}\"."),
 ("AutoStoppedAfter", "Stopped after {0} of {1}.", "Parado depois de {0} de {1}."),
 ("AutoStoppedAtStepMissing", "Stopped at step {0} - no command called \"{1}\".",
  "Parado no passo {0} - não existe comando chamado \"{1}\"."),
 ("AutoStepOf", "Step {0} of {1}: {2}", "Passo {0} de {1}: {2}"),
 ("AutoStoppedAtStepFailed", "Stopped at step {0} of {1}: {2} failed.",
  "Parado no passo {0} de {1}: {2} falhou."),
 ("AutoStoppedAtStep", "Stopped at step {0} of {1}.", "Parado no passo {0} de {1}."),
 ("AutoNothingToRun", "Nothing to run - this chain has no steps.",
  "Nada a executar - esta sequência não tem passos."),
 ("AutoFinishedAll", "Finished all {0} steps.", "Terminou todos os {0} passos."),
 ("AutoOutputStep", "Step {0} of {1} · {2}", "Passo {0} de {1} · {2}"),
 ("AutoOutputExited", "{0} exited with code {1}.", "{0} terminou com código {1}."),
 ("AutoOutputSkipped", "Skipped - no command called \"{0}\".",
  "Ignorado - não existe comando chamado \"{0}\"."),
 ("AutoWaitingFor", "Step {0} of {1}: waiting for {2}, which is already running.",
  "Passo {0} de {1}: aguardando {2}, que já está em execução."),
 ("AutoWhenFilesChange", "When files change", "Quando arquivos mudarem"),
 ("AutoNothingToImport", "Nothing to import - this project has no deck file, or it could not be read.",
  "Nada a importar - este projeto não tem arquivo de deck, ou ele não pôde ser lido."),
 ("AutoDeckEmpty", "That deck file lists nothing runnable.",
  "Esse arquivo de deck não lista nada executável."),
 ("AutoDeckAllPresent", "Everything in that deck file is already in your deck.",
  "Tudo nesse arquivo de deck já está no seu deck."),
 ("AutoImportedOne", "Imported 1 command.", "1 comando importado."),
 ("AutoImportedMany", "Imported {0} commands.", "{0} comandos importados."),
 ("AutoImportedSome", "Imported {0}, and {1} were already here.",
  "{0} importados, e {1} já estavam aqui."),
 ("AutoWroteDeck", "Wrote {0} commands to {1}. Commit it and the next person has your deck.",
  "{0} comandos escritos em {1}. Versione o arquivo e a próxima pessoa terá o seu deck."),
 ("AutoCouldNotWrite", "Could not write it: {0}", "Não foi possível escrever: {0}"),

 # --- Commands, HTTP and the smaller panels, at runtime ----------------------
 ("CmdHaveAllStarters", "You already have all of the starter commands.",
  "Você já tem todos os comandos iniciais."),
 ("CmdAddedOneStarter", "Added 1 starter command.", "1 comando inicial adicionado."),
 ("CmdAddedStarters", "Added {0} starter commands.", "{0} comandos iniciais adicionados."),
 ("CmdChooseWorkspaceFirst", "Choose a workspace first - there is nothing to read yet.",
  "Escolha primeiro uma área de trabalho - ainda não há nada para ler."),
 ("CmdNothingFound", "Nothing found. This project declares no scripts this knows how to read.",
  "Nada encontrado. Este projeto não declara scripts que isto saiba ler."),
 ("CmdAllPresent", "Everything this project declares is already in your deck.",
  "Tudo que este projeto declara já está no seu deck."),
 ("CmdImportedOne", "Imported 1 command.", "1 comando importado."),
 ("CmdImportedMany", "Imported {0} commands.", "{0} comandos importados."),
 ("CmdImportedSome", "Imported {0}, and {1} were already here.",
  "{0} importados, e {1} já estavam aqui."),
 ("CmdCopiedLink", "Copied a link that runs \"{0}\".", "Link copiado que executa \"{0}\"."),
 ("CmdNewCommand", "New command", "Novo comando"),
 ("CmdInUse",
  "Used by {0}. Take it out of there first, then it can be deleted.",
  "Usado por {0}. Tire-o de lá primeiro, depois ele pode ser excluído."),
 ("CmdInUseChain", "the chain \"{0}\"", "a sequência \"{0}\""),
 ("CmdInUseWatch", "the watch \"{0}\"", "o monitoramento \"{0}\""),
 ("CmdStopAllCount", "Stop all ({0})", "Parar tudo ({0})"),
 ("CmdStopAll", "Stop all", "Parar tudo"),
 ("CmdNotRunYet", "Not run yet.", "Ainda não executado."),
 ("CmdUsually", "usually {0}", "normalmente {0}"),
 ("CmdStartedDetached", "Started. Not waiting for it to finish.",
  "Iniciado. Sem esperar terminar."),
 ("CmdStartedDetachedTail", "Started detached. Nothing further will be reported.",
  "Iniciado em segundo plano. Nada mais será relatado."),
 ("CmdExitAfter", "Exit code {0} after {1}", "Código de saída {0} depois de {1}"),
 ("CmdStoppedAfter", "Stopped after {0}", "Parado depois de {0}"),
 ("CmdCouldNotStart", "Could not start: {0}", "Não foi possível iniciar: {0}"),

 ("HttpNewRequestName", "New request", "Nova requisição"),
 ("HttpNoEnvironmentFor", "No environment chosen, so {0} will be sent as written.",
  "Nenhum ambiente escolhido, então {0} será enviado como está escrito."),
 ("HttpEnvironmentMissing", "{0} does not define {1}.", "{0} não define {1}."),
 ("HttpImportedNote", "Imported {0} · ", "{0} importado · "),
 ("HttpOneHeader", "1 header", "1 cabeçalho"),
 ("HttpManyHeaders", "{0} headers", "{0} cabeçalhos"),
 ("HttpNoteCookies", " · cookies", " · cookies"),
 ("HttpNoteBody", " · body", " · corpo"),
 ("HttpNoteCredentials", ". Credentials came with it - saved as typed.",
  ". Credenciais vieram junto - salvas como foram digitadas."),
 ("HttpFiledUnder", "Filed under {0}.", "Arquivado em {0}."),
 ("HttpUngrouped", "Ungrouped.", "Sem grupo."),
 ("HttpCopySuffix", "{0} copy", "{0} cópia"),
 ("HttpLastOne", "That is the last one - clear it instead.",
  "Esta é a última - limpe-a em vez de excluir."),
 ("HttpCurlCopied", "curl command copied.", "Comando curl copiado."),
 ("HttpNotCurl", "That did not look like a curl command.",
  "Isso não parecia um comando curl."),
 ("HttpResponseCopied", "Response copied.", "Resposta copiada."),
 ("HttpMovedToEnvironment", " {0} moved into {1}.", " {0} movido para {1}."),
 ("HttpSave", "Save", "Salvar"),
 ("HttpSaveResponse", "Save the response to a file", "Salvar a resposta em um arquivo"),
 ("HttpResponseSaved", "Saved as {0}.", "Salvo como {0}."),
 ("HttpResponseNotSaved", "Could not save: {0}", "Não foi possível salvar: {0}"),
 ("HttpImageBroken", "This image could not be decoded. Save it and open it elsewhere.", "Não foi possível decodificar esta imagem. Salve-a e abra em outro programa."),
 ("HttpMediaHere", "Audio and video are not played here. Save the file and open it in a player.", "Áudio e vídeo não são reproduzidos aqui. Salve o arquivo e abra em um reprodutor."),

 ("ClipsRecordingOn", "Recording what you copy. Anything that looks like a credential is skipped.",
  "Registrando o que você copia. Qualquer coisa que pareça uma credencial é ignorada."),
 ("ClipsRecordingOff", "Stopped. What was already recorded is still here until you clear it.",
  "Parado. O que já foi registrado continua aqui até você limpar."),

 ("LogCopiedLines", "Copied {0} lines.", "{0} linhas copiadas."),

 ("RunContainersVia", "Containers, through {0}.", "Contêineres, via {0}."),
 ("RunNoEngine", "No container engine found. Install Docker or Podman and restart to see containers here.",
  "Nenhum engine de contêineres encontrado. Instale o Docker ou o Podman e reinicie para ver contêineres aqui."),
 ("RunSummary", "{0} listening · {1} containers · checked {2}",
  "{0} escutando · {1} contêineres · verificado às {2}"),
 ("RunAddedLogs", "Added \"logs: {0}\" to the deck.", "\"logs: {0}\" adicionado ao deck."),

 ("ParamDefaultsTo", "Defaults to {0}", "Padrão: {0}"),
 ("ParamOffending", "This contains {0}, so it would run as more than one command.",
  "Isto contém {0}, então rodaria como mais de um comando."),

 # --- Memory, at runtime -----------------------------------------------------
 ("MemPid", "pid {0}", "pid {0}"),
 ("MemDetail", "{0} · pid {1} · {2} · {3}% of physical memory",
  "{0} · pid {1} · {2} · {3}% da memória física"),
 ("MemCores", "{0} logical cores", "{0} núcleos lógicos"),
 ("MemCleaning", "Cleaning…", "Limpando…"),
 ("MemCleanLabel", "Clean memory", "Limpar memória"),
 ("MemNothingTicked", "No steps are ticked, so there was nothing to run.",
  "Nenhum passo está marcado, então não havia nada a executar."),
 ("MemFreedSummary", "{0} · {1}", "{0} · {1}"),
 ("MemCleanFailed", "The clean failed: {0}", "A limpeza falhou: {0}"),
 ("MemRunningStep", "Running: {0}…", "Executando: {0}…"),
 ("MemStepOutcome", "{0} — {1}", "{0} — {1}"),
 ("MemFreed", "Freed {0}", "{0} liberados"),
 ("MemLessAvailable", "{0} less available", "{0} a menos disponíveis"),
 ("MemNoChange", "No change", "Sem mudança"),
 ("MemStepsRanOne", "{0} of {1} step ran", "{0} de {1} passo executado"),
 ("MemStepsRan", "{0} of {1} steps ran", "{0} de {1} passos executados"),
 ("MemUnreadable", "Could not read memory on this system.",
  "Não foi possível ler a memória neste sistema."),
 ("MemInUse", "{0} of {1} in use · processor {2}", "{0} de {1} em uso · processador {2}"),
 ("MemHideWidget", "Hide widget", "Ocultar o widget"),
 ("MemFloatWidget", "Float widget", "Destacar o widget"),

 # --- Settings, at runtime ---------------------------------------------------
 ("SetHotkeyHeld", "{0} brings the deck up from anywhere.",
  "{0} traz o deck à frente de qualquer lugar."),
 ("SetStoragePortable", "Beside the executable, so a copied folder carries your deck with it.",
  "Ao lado do executável, então uma pasta copiada leva o seu deck junto."),
 ("SetStorageConfig", "In your own config folder, which is where a signed or system-installed build has to keep it.",
  "Na sua própria pasta de configuração, que é onde uma versão assinada ou instalada pelo sistema precisa guardar."),
 ("SetSavedAutomatically", "Saved automatically.", "Salvo automaticamente."),
 ("SetSecretNeedsBoth", "A secret needs both a name and a value.",
  "Um segredo precisa de um nome e de um valor."),
 ("SetSecretSaved", "Saved. Use it in a command as {{{{secret:{0}}}}}.",
  "Salvo. Use em um comando como {{{{secret:{0}}}}}."),
 ("SetSecretRemoved", "Removed {0}. Any command using it will now run with it left as typed.",
  "{0} removido. Qualquer comando que o use agora vai rodar com ele como foi digitado."),
 ("SetSchemeCan", "Registers devdeck:// for your user only, so a link in a README or a CI page opens this deck.",
  "Registra devdeck:// só para o seu usuário, para que um link num README ou numa página de CI abra este deck."),
 ("SetSchemeCannot", "On macOS this belongs to an app bundle's Info.plist and cannot be claimed by a running process. The terminal launcher works either way.",
  "No macOS isto pertence ao Info.plist de um pacote de aplicativo e não pode ser reivindicado por um processo em execução. O atalho de terminal funciona de qualquer forma."),
 ("SetHotkeyNote", "Works while you are in another application. A modifier is required - Ctrl, Alt, Shift or Win - so the key still does its usual job on its own.",
  "Funciona enquanto você está em outro aplicativo. Um modificador é obrigatório - Ctrl, Alt, Shift ou Win - para que a tecla continue fazendo o trabalho dela sozinha."),
 ("SetHotkeyUnreadable", "\"{0}\" is not a combination I can read. Try something like Ctrl+Shift+D.",
  "\"{0}\" não é uma combinação que eu consiga ler. Tente algo como Ctrl+Shift+D."),
 ("SetHotkeyTaken", "Could not claim {0} - something else on this machine already has it. Try another combination.",
  "Não foi possível reservar {0} - algo mais nesta máquina já tem essa combinação. Tente outra."),
 ("SetHotkeyClaimed", "{0} now brings the deck up from anywhere.",
  "{0} agora traz o deck à frente de qualquer lugar."),
 ("SetHotkeyNone", "No key is claimed.", "Nenhuma tecla está reservada."),
 ("SetNoRelease", "Nothing published yet at the release page.",
  "Nada publicado ainda na página de versões."),
 ("SetUpdateAvailable", "{0} is available. You have {1}.", "{0} está disponível. Você tem {1}."),
 ("SetUpToDate", "You have the newest build ({0}).", "Você tem a versão mais nova ({0})."),
 ("SetCouldNotCheck", "Could not check: {0}", "Não foi possível verificar: {0}"),
 ("SetSavedAt", "Saved at {0}.", "Salvo às {0}."),

 ("PaletteCategoryRun", "Run", "Executar"),
 ("PaletteCategoryGoTo", "Go to", "Ir para"),
 ("PaletteCategoryDeck", "Deck", "Deck"),
 ("PaletteCategoryChain", "Chain", "Sequência"),
 ("PaletteCategoryAutomation", "Automation", "Automação"),
 ("PaletteNewCommand", "New command", "Novo comando"),
 ("PaletteStopEverything", "Stop everything that is running", "Parar tudo que está rodando"),
 ("PaletteImportRunnables", "Import runnables from this project", "Importar executáveis deste projeto"),
 ("PaletteImportRunnablesHint", "package.json, Makefile, Cargo.toml and the rest",
  "package.json, Makefile, Cargo.toml e o resto"),
 ("PaletteNewChain", "New chain", "Nova sequência"),
 ("PaletteNewChainHint", "Several commands, run one after another",
  "Vários comandos, executados um depois do outro"),

 ("NavFinished", "finished", "terminou"),
 ("NavWasStopped", "was stopped", "foi interrompido"),
 ("NavFailed", "failed", "falhou"),
 ("NavRanSummary", "{0}s · exit code {1}", "{0}s · código de saída {1}"),
 ("PaletteExportDeckHint", "Writes {0} for the next person to clone it",
  "Escreve {0} para a próxima pessoa que clonar o projeto"),
 ("PaletteImportDeck", "Import this project's deck file", "Importar o arquivo de deck deste projeto"),
 ("PaletteExportDeck", "Export deck to this project", "Exportar o deck para este projeto"),

 ("NavCommands", "Commands", "Comandos"),
 ("NavCommandsBlurb", "Your saved commands and scripts.", "Seus comandos e scripts salvos."),
 ("NavAssistant", "Assistant", "Assistente"),
 ("NavAssistantBlurb", "A local or hosted model, and its code straight into the deck.",
  "Um modelo local ou hospedado, e o código dele direto no deck."),
 ("NavHttp", "HTTP", "HTTP"),
 ("NavHttpBlurb", "Send a request. Paste a curl command and it becomes one.",
  "Envie uma requisição. Cole um comando curl e ele vira uma."),
 ("NavToolbox", "Toolbox", "Ferramentas"),
 ("NavToolboxBlurb", "Encode, decode, format and hash - all of it on this machine.",
  "Codificar, decodificar, formatar e gerar hash - tudo nesta máquina."),
 ("NavRunning", "Running", "Em execução"),
 ("NavRunningBlurb", "What is listening, and what is containerised.",
  "O que está escutando, e o que está em contêineres."),
 ("NavAutomation", "Automation", "Automação"),
 ("NavAutomationBlurb", "Chains, file watches and the deck a project commits.",
  "Sequências, monitoramento de arquivos e o deck que um projeto versiona."),
 ("NavClipboard", "Clipboard", "Área de transferência"),
 ("NavClipboardBlurb", "What you have copied, once you switch it on.",
  "O que você copiou, depois de você ligar isso."),
 ("NavLog", "Log", "Registro"),
 ("NavLogBlurb", "Everything the app itself has done this session.",
  "Tudo que o próprio app fez nesta sessão."),
 ("NavMemory", "Memory/CPU", "Memória/CPU"),
 ("NavMemoryBlurbWindows", "Memory and processor load, working set trimming and the standby cache.",
  "Carga de memória e processador, redução do working set e o cache de espera."),
 ("NavMemoryBlurbOther", "Memory and processor load. Only the page cache can be dropped on this platform.",
  "Carga de memória e processador. Nesta plataforma só o cache de páginas pode ser descartado."),
 ("NavSettings", "Settings", "Configurações"),
 ("NavSettingsBlurb", "Theme, behaviour and where this build keeps its files.",
  "Tema, comportamento e onde esta versão guarda seus arquivos."),

 ("AssistantAssistant", "Assistant", "Assistente"),
 ("AssistantHistory", "History", "Histórico"),
 ("Delete", "Delete", "Excluir"),
 ("SelectText", "Select text", "Selecionar texto"),
 ("SelectTextTip",
  "Shows the output as plain text that can be selected across lines and copied. It is a snapshot: turn it off to see new output, in colour, again.",
  "Mostra a saída como texto simples, que pode ser selecionado entre linhas e copiado. É um retrato do momento: desligue para ver a saída nova, com cores, de novo."),
 ("AssistantSaveAsCommand", "Save as command", "Salvar como comando"),
 ("AssistantNewChat", "New chat", "Nova conversa"),
 ("AssistantHelp", "Help", "Ajuda"),
 ("AiHelpTitle", "What the assistant can do", "O que o assistente pode fazer"),
 ("AiHelpIntro",
  "Ask in plain words. Code it writes comes back with buttons to run it or keep it, and nothing runs until you say so.",
  "Pergunte com suas palavras. O código que ele escreve vem com botões para executar ou guardar, e nada executa até você mandar."),
 ("AiHelpUseExample", "Puts this in the box below, ready to send or change.", "Coloca isto na caixa abaixo, pronto para enviar ou alterar."),
 ("AiHelpCommandTitle", "Write a command", "Escrever um comando"),
 ("AiHelpCommandText",
  "A one-liner or a whole script, in a language this machine runs. Run it here and read the output in the chat, or Add as command to keep it in the deck.",
  "Uma linha ou um script inteiro, numa linguagem que esta máquina executa. Execute aqui e leia a saída na conversa, ou use Adicionar como comando para guardá-lo no deck."),
 ("AiHelpCommandExample",
  "Write a script that lists the 10 largest files under this folder",
  "Escreva um script que liste os 10 maiores arquivos dentro desta pasta"),
 ("AiHelpChainTitle", "Build a chain", "Montar uma sequência"),
 ("AiHelpChainText",
  "Ask for several commands that work together. Each comes back as its own block, followed by a chain block: Create chain adds the commands to the deck and the chain to Automation. Later steps can read what earlier ones printed.",
  "Peça vários comandos que trabalhem juntos. Cada um vem num bloco próprio, seguido de um bloco de sequência: Criar sequência adiciona os comandos ao deck e a sequência à Automação. Os passos seguintes podem ler o que os anteriores imprimiram."),
 ("AiHelpChainExample",
  "Make three commands that count the files, count the TODOs and show the git branch, and a chain that writes a short report from their output",
  "Crie três comandos que contem os arquivos, contem os TODOs e mostrem o branch do git, e uma sequência que escreva um relatório curto com a saída deles"),
 ("AiHelpFailureTitle", "Explain a failure", "Explicar uma falha"),
 ("AiHelpFailureText",
  "When a command fails on the Commands page, Explain this failure sends it here with its output already attached. You can also paste an error and ask.",
  "Quando um comando falha na página Comandos, Explicar esta falha o envia para cá com a saída já anexada. Você também pode colar um erro e perguntar."),
 ("AiHelpFailureExample",
  "Why would 'dotnet build' fail with error NETSDK1045, and how do I fix it?",
  "Por que o 'dotnet build' falharia com o erro NETSDK1045, e como eu corrijo?"),
 ("AiHelpFilesTitle", "Ask about your files", "Perguntar sobre seus arquivos"),
 ("AiHelpFilesText",
  "Attach files, or drag them onto the chat, and ask about them. It also knows which workspace folder you are in.",
  "Anexe arquivos, ou arraste-os para a conversa, e pergunte sobre eles. Ele também sabe em qual pasta de trabalho você está."),
 ("AiHelpFilesExample",
  "Explain what the attached script does, and what could go wrong when it runs",
  "Explique o que o script anexado faz, e o que pode dar errado quando ele executar"),
 ("AiHelpRefineTitle", "Change what it wrote", "Alterar o que ele escreveu"),
 ("AiHelpRefineText",
  "Follow up on the last answer instead of starting again: it remembers the recent conversation.",
  "Continue a partir da última resposta em vez de recomeçar: ele se lembra da conversa recente."),
 ("AiHelpRefineExample",
  "Make the last script skip the bin, obj and node_modules folders",
  "Faça o último script ignorar as pastas bin, obj e node_modules"),
 ("AiHelpTipsTitle", "Getting good answers", "Para ter boas respostas"),
 ("AiHelpTip1",
  "Say what you want to end up with, not just the tool: \"the branches already merged into main\" beats \"a git command\".",
  "Diga o que você quer obter, não só a ferramenta: \"os branches já mesclados na main\" é melhor que \"um comando git\"."),
 ("AiHelpTip2",
  "Name the language when it matters: PowerShell, batch or bash.",
  "Diga a linguagem quando fizer diferença: PowerShell, batch ou bash."),
 ("AiHelpTip3",
  "For a chain, say what each step should produce and what the last step should do with it.",
  "Para uma sequência, diga o que cada passo deve produzir e o que o último passo deve fazer com isso."),
 ("AiHelpTip4",
  "Read code before you run it, above all anything that deletes, moves or installs. You are asked once per session before code runs.",
  "Leia o código antes de executar, principalmente o que apaga, move ou instala. Você é consultado uma vez por sessão antes de o código executar."),
 ("AiHelpTip5",
  "Keep a prompt you reuse with Keep this prompt and find it under Prompts. History reopens earlier chats.",
  "Guarde um prompt que você reutiliza com Guardar este prompt e encontre-o em Prompts. Histórico reabre conversas anteriores."),
 ("AiHelpTip6",
  "Choose the provider and model under Settings > AI. Larger models follow the chain format more reliably than small local ones.",
  "Escolha o provedor e o modelo em Configurações > IA. Modelos maiores seguem o formato de sequência com mais confiabilidade que os locais pequenos."),
 ("AssistantAiSettings", "AI settings", "Configurações de IA"),
 ("AssistantProvider", "Provider", "Provedor"),
 ("AssistantEndpoint", "Endpoint", "Endpoint"),
 ("AssistantModel", "Model", "Modelo"),
 ("Refresh", "Refresh", "Atualizar"),
 ("AssistantAPIKey", "API key", "Chave da API"),
 ("AssistantCheckAgain", "Check again", "Verificar de novo"),
 ("AssistantCopyThisMessage", "Copy this message", "Copiar esta mensagem"),
 ("AssistantCopyWholeConversation", "Copy whole conversation", "Copiar a conversa inteira"),
 ("AssistantAddAsCommand", "Add as command", "Adicionar como comando"),
 ("AssistantCreateChain", "Create chain", "Criar sequência"),
 ("AssistantAskItSomething", "Ask it something.", "Pergunte alguma coisa."),
 ("AssistantAnyCodeItWritesGetsA",
  "Any code it writes gets a Run button and an Add as command button. Run streams the output straight back into the conversation.",
  "Todo código que ele escrever ganha um botão Executar e um botão Adicionar como comando. Executar traz a saída direto de volta para a conversa."),
 ("AssistantRunThisScriptNow", "Run this script now?", "Executar este script agora?"),
 ("AssistantThisIsAskedOncePerSession", "This is asked once per session.", "Isto é perguntado uma vez por sessão."),
 ("AssistantRunIt", "Run it", "Executar"),
 ("Cancel", "Cancel", "Cancelar"),
 ("AssistantAttachAFile", "Attach a file", "Anexar um arquivo"),
 ("AssistantPrompts", "Prompts", "Prompts"),
 ("AssistantKeepThisPrompt", "Keep this prompt", "Guardar este prompt"),
 ("AssistantAskTheModel", "Ask the model…", "Pergunte ao modelo…"),
 ("Send", "Send", "Enviar"),
 ("Stop", "Stop", "Parar"),
 ("AssistantStopTheScript", "Stop the script", "Parar o script"),

 ("AutomationAutomation", "Automation", "Automação"),
 ("AutomationEverythingThatRunsACommandWithout",
  "Everything that runs a command without you pressing Run.",
  "Tudo que executa um comando sem você apertar Executar."),
 ("AutomationChains", "Chains", "Sequências"),
 ("AutomationSeveralCommandsOneAfterAnotherEach",
  "Several commands, one after another. Each step waits for the last.",
  "Vários comandos, um depois do outro. Cada passo espera o anterior."),
 ("AutomationNewChain", "New chain", "Nova sequência"),
 ("AutomationNoChainsYetAChainIs",
  "No chains yet. A chain is a list of command names - pull, build, test - run in order, stopping at the first failure.",
  "Nenhuma sequência ainda. Uma sequência é uma lista de nomes de comandos - pull, build, test - executados em ordem, parando na primeira falha."),
 ("AutomationBroken", "broken", "quebrado"),
 ("Name", "Name", "Nome"),
 ("AutomationStepsInOrder", "Steps, in order", "Passos, em ordem"),
 ("NavSectionBusy", "Running", "Executando"),
 ("HttpHeaderCount", "{0} headers", "{0} cabe\u00e7alhos"),
 ("HttpHeaderCountOne", "1 header", "1 cabe\u00e7alho"),
 ("HttpFilterHeaders", "Filter", "Filtrar"),
 ("HttpNoHeadersMatch", "No header matches that.", "Nenhum cabe\u00e7alho corresponde a isso."),
 ("HttpNoHeadersAtAll", "The reply carried no headers.", "A resposta n\u00e3o trouxe cabe\u00e7alhos."),
 ("HttpCopyHeader", "Copy this header", "Copiar este cabe\u00e7alho"),
 ("IconPickerTitle", "Choose an icon", "Escolher um \u00edcone"),
 ("IconPickerPickOne",
  "Pick one to mark this in the list. Hover a tile to see the name it is stored under.",
  "Escolha um para marcar isto na lista. Passe o mouse sobre um \u00edcone para ver o nome com que ele \u00e9 salvo."),
 ("IconPickerNone", "No icon", "Sem \u00edcone"),
 ("HttpChooseIcon", "Icon...", "\u00cdcone..."),
 ("HttpGroupIcon", "Group icon...", "\u00cdcone do grupo..."),
 ("AutomationNoStepsYet",
  "No steps yet. Pick a command below and add it.",
  "Nenhum passo ainda. Escolha um comando abaixo e adicione."),
 ("AutomationAddStep", "Add step", "Adicionar passo"),
 ("AutomationMoveStepUp", "Move this step earlier", "Mover este passo para antes"),
 ("AutomationMoveStepDown", "Move this step later", "Mover este passo para depois"),
 ("AutomationRemoveStep", "Remove this step", "Remover este passo"),
 ("AutomationStepIsMissing",
  "No command by this name",
  "Nenhum comando com este nome"),
 ("AutomationSameStepTwiceIsFine",
  "A command may appear more than once - build, test, build is a real chain.",
  "Um comando pode aparecer mais de uma vez - build, test, build \u00e9 uma sequ\u00eancia leg\u00edtima."),
 ("AutomationStopAtTheFirstStepThat", "Stop at the first step that fails", "Parar no primeiro passo que falhar"),
 ("AutomationOffIsForAChainThat",
  "Off is for a chain that is a list of chores rather than a pipeline - one of them failing is not a reason to skip the rest.",
  "Desligado é para uma sequência que é uma lista de tarefas e não um pipeline - uma delas falhar não é motivo para pular as outras."),
 ("AutomationRunChain", "Run chain", "Executar sequência"),
 ("AutomationChainOutput", "Output", "Saída"),
 ("AutomationChainOutputEmpty", "Run the chain and every step's output appears here, one after another.",
  "Execute a sequência e a saída de cada passo aparece aqui, uma após a outra."),
 ("AutomationFold", "Show or hide this section", "Mostrar ou ocultar esta seção"),
 ("Save", "Save", "Salvar"),
 ("AutomationWhenFilesChange", "When files change", "Quando arquivos mudarem"),
 ("AutomationRunACommandWhenSomethingIn",
  "Run a command when something in a folder is saved.",
  "Executar um comando quando algo numa pasta for salvo."),
 ("AutomationNewWatch", "New watch", "Novo monitoramento"),
 ("AutomationAWatchWaitsForTheWriting",
  "A watch waits for the writing to stop before it runs anything, ignores bin, obj, .git and node_modules, and will not start a command that is already running.",
  "O monitoramento espera a escrita parar antes de executar qualquer coisa, ignora bin, obj, .git e node_modules, e não inicia um comando que já está rodando."),
 ("AutomationNoWatchesYet", "No watches yet.", "Nenhum monitoramento ainda."),
 ("AutomationOn", "On", "Ligado"),
 ("AutomationCommandToRunByName", "Command to run", "Comando a executar"),
 ("AutomationPickACommand", "Pick a command", "Escolha um comando"),
 ("AutomationBrowse", "Browse...", "Procurar..."),
 ("AutomationPickTheFolderToWatch", "Pick the folder to watch", "Escolha a pasta para monitorar"),
 ("AutoWatchFired", "{0} fired", "{0} disparou"),
 ("AutoRunningBecause", "{0} changed - running {1}", "{0} mudou - executando {1}"),
 ("AutoSomethingChanged", "something", "algo"),
 ("AutoAndOthers", "{0} and {1} more", "{0} e mais {1}"),
 ("AutoRanAtFor", "Ran at {0} for {1} · {2} so far", "Executado às {0} por {1} · {2} até agora"),
 ("AutomationTellMeWhenAWatchFires", "Tell me when a watch fires",
  "Avisar quando um monitoramento disparar"),
 ("AutomationWhatAWatchPassesToItsCommand",
  "A watch passes what changed to its command as environment variables: DEVDECK_CHANGED_FILE, DEVDECK_CHANGED_NAME, DEVDECK_CHANGED_FILES (one path per line), DEVDECK_CHANGED_COUNT, DEVDECK_CHANGED_KIND and DEVDECK_WATCH_NAME. The starter command \"What changed just now\" prints all of them.",
  "Um monitoramento passa o que mudou para o comando como variáveis de ambiente: DEVDECK_CHANGED_FILE, DEVDECK_CHANGED_NAME, DEVDECK_CHANGED_FILES (um caminho por linha), DEVDECK_CHANGED_COUNT, DEVDECK_CHANGED_KIND e DEVDECK_WATCH_NAME. O comando inicial \"What changed just now\" imprime todos eles."),
 ("AutomationFolderEmptyFollowsTheWorkspace",
  "Folder - empty follows the workspace", "Pasta - vazio segue a área de trabalho"),
 ("AutomationFilesSeparatedBySemicolons", "Files, separated by semicolons", "Arquivos, separados por ponto e vírgula"),
 ("AutomationThisProjectSOwnDeck", "This project's own deck", "O deck do próprio projeto"),
 ("AutomationADevdeckJsonCommittedBesideThe",
  "A .devdeck.json committed beside the code, so the commands arrive with it.",
  "Um .devdeck.json versionado junto do código, para os comandos chegarem junto."),
 ("AutomationReadingADeckFileNeverRuns",
  "Reading a deck file never runs anything. Importing adds its commands to your deck, where you decide what to run.",
  "Ler um arquivo de deck nunca executa nada. Importar adiciona os comandos dele ao seu deck, onde você decide o que executar."),
 ("ImportFromThisProject", "Import from this project", "Importar deste projeto"),
 ("AutomationExportMyDeckToThisProject", "Export my deck to this project", "Exportar meu deck para este projeto"),

 ("ClipsClipboard", "Clipboard", "Área de transferência"),
 ("ClipsWhatYouHaveCopiedWhileThis",
  "What you have copied, while this panel was open.",
  "O que você copiou enquanto este painel esteve aberto."),
 ("ClipsRecordWhatICopy", "Record what I copy", "Registrar o que eu copiar"),
 ("ClipsThisIsOffUntilYouTurn",
  "This is off until you turn it on, and it only runs while you are on this page. Values that look like tokens, keys or passwords are skipped rather than recorded - but that check is a guess, not a guarantee.",
  "Isto fica desligado até você ligar, e só funciona enquanto você está nesta página. Valores que parecem tokens, chaves ou senhas são ignorados em vez de registrados - mas essa checagem é um palpite, não uma garantia."),
 ("ClipsSearchWhatYouHaveCopied", "Search what you have copied", "Buscar no que você copiou"),
 ("Copy", "Copy", "Copiar"),
 ("ClipsPin", "Pin", "Fixar"),
 ("ClipsClearAll", "Clear all", "Limpar tudo"),
 ("ClipsOpenThisInTheBrowser", "Open this in the browser", "Abrir isto no navegador"),
 ("ClipsLink", "link", "link"),
 ("ClipsNothingRecordedYet", "Nothing recorded yet.", "Nada registrado ainda."),

 ("CommandsCommands", "Commands", "Comandos"),
 ("CommandsWorkspace", "Workspace", "Área de trabalho"),
 ("CommandsNoFolderChosenYet", "No folder chosen yet", "Nenhuma pasta escolhida ainda"),
 ("CommandsBrowse", "Browse", "Procurar"),
 ("CommandsNewCommand", "New command", "Novo comando"),
 ("CommandsReadsPackageJsonMakefileCargoToml",
  "Reads package.json, Makefile, Cargo.toml, compose files and the rest",
  "Lê package.json, Makefile, Cargo.toml, arquivos compose e o resto"),
 ("CommandsAddTheStarterCommands", "Add the starter commands", "Adicionar os comandos iniciais"),
 ("CommandsCopyALinkToThisCommand", "Copy a link to this command", "Copiar um link para este comando"),
 ("CommandsDevdeckRunOpensTheDeckAnd",
  "devdeck://run/… - opens the deck and runs it",
  "devdeck://run/… - abre o deck e executa"),
 ("CommandsNoCommandsYetAddOneAnd",
  "No commands yet. Add one and it is saved with your settings, body and all.",
  "Nenhum comando ainda. Adicione um e ele é salvo junto com suas configurações, corpo e tudo."),
 ("CommandsRunWith", "Run with", "Executar com"),
 ("CommandsBody", "Body", "Corpo"),
 ("CommandsWriteNameOrNameDefaultTo",
  "Write {{name}} or {{name:default}} to be asked for a value before this runs. {{secret:NAME}} is filled from the vault and never shown.",
  "Escreva {{name}} ou {{name:padrão}} para que um valor seja pedido antes de executar. {{secret:NOME}} é preenchido pelo cofre e nunca é exibido."),
 ("Run", "Run", "Executar"),
 ("CommandsExplainThisFailure", "Explain this failure", "Explicar esta falha"),
 ("CommandsRunAndDonTWaitFor", "Run and don't wait for it to finish", "Executar sem esperar terminar"),
 ("CommandsFollowOutput", "Follow output", "Acompanhar a saída"),
 ("CommandsOpenInYourEditor", "Open in your editor", "Abrir no seu editor"),
 ("CommandsOutputAppearsHereOnceYouRun",
  "Output appears here once you run this command.",
  "A saída aparece aqui depois que você executar este comando."),
 ("CommandsChooseAWorkspaceFirst", "Choose a workspace first", "Escolha uma área de trabalho primeiro"),
 ("CommandsEveryCommandRunsInsideThisFolder",
  "Every command runs inside this folder, so there is nothing sensible to run until one is picked. Choose the repository or project you are working in and the deck opens.",
  "Todo comando roda dentro desta pasta, então não há nada sensato a executar até escolher uma. Escolha o repositório ou projeto em que você está trabalhando e o deck abre."),
 ("CommandsChooseAFolder", "Choose a folder", "Escolher uma pasta"),


 # --- Built in code-behind rather than in a view ---
 ("PickAttachFiles", "Attach files to the conversation",
  "Anexar arquivos à conversa"),
 ("PickWorkspace", "Choose the folder commands run in",
  "Escolha a pasta onde os comandos rodam"),
 ("EnvSecretNote", "A value marked secret is kept in this machine's vault, not in settings.json.",
  "Um valor marcado como secreto fica no cofre desta máquina, não no settings.json."),
 ("EnvNewName", "New environment", "Novo ambiente"),
 ("EnvUseThisValue", "Use this value", "Usar este valor"),
 ("EnvKeepInVault", "Keep this value in the vault rather than in settings.json",
  "Guardar este valor no cofre em vez de no settings.json"),
 ("EnvStoredTypeToReplace", "stored - type to replace",
  "guardado - digite para substituir"),
 ("EnvNotSet", "not set", "não definido"),
 ("EnvName", "name", "nome"),
 ("EnvValue", "value", "valor"),
 ("EnvSecret", "secret", "secreto"),
 ("HttpAskName", "What should this request be called?",
  "Como esta requisição deve se chamar?"),
 ("HttpAskGroup", "Which group should this be filed under?",
  "Em qual grupo isto deve ficar?"),
 ("EnvironmentEnvironments", "Environments", "Ambientes"),
 ("EnvironmentASetOfValuesPerEnvironment",
  "A set of values per environment. Write {{name}} in a URL, a header or a body and it is filled in from whichever environment is chosen when the request is sent.",
  "Um conjunto de valores por ambiente. Escreva {{nome}} numa URL, num cabeçalho ou num corpo e ele é preenchido a partir do ambiente escolhido na hora de enviar."),
 ("EnvironmentAdd", "Add", "Adicionar"),
 ("Remove", "Remove", "Remover"),
 ("EnvironmentEnvironmentName", "Environment name", "Nome do ambiente"),
 ("EnvironmentAddValue", "+ Add value", "+ Adicionar valor"),
 ("EnvironmentDone", "Done", "Pronto"),

 ("HttpRename", "Rename…", "Renomear…"),
 ("HttpNameItAfterTheURL", "Name it after the URL", "Nomear pela URL"),
 ("HttpDuplicate", "Duplicate", "Duplicar"),
 ("HttpFlag", "Flag", "Marcador"),
 ("HttpNoFlag", "No flag", "Sem marcador"),
 ("HttpRed", "Red", "Vermelho"),
 ("HttpAmber", "Amber", "Âmbar"),
 ("HttpGreen", "Green", "Verde"),
 ("HttpBlue", "Blue", "Azul"),
 ("HttpPurple", "Purple", "Roxo"),
 ("HttpGrey", "Grey", "Cinza"),
 ("HttpMoveToGroup", "Move to group", "Mover para grupo"),
 ("HttpNoGroup", "No group", "Sem grupo"),
 ("HttpNewGroup", "New group…", "Novo grupo…"),
 ("HttpExisting", "Existing", "Existentes"),
 ("HttpCopyAsCurl", "Copy as curl", "Copiar como curl"),
 ("HttpDeleteThisRequest", "Delete this request", "Excluir esta requisição"),
 ("HttpNewRequest", "New request", "Nova requisição"),
 ("HttpPasteACurlCommand", "Paste a curl command", "Colar um comando curl"),
 ("HttpEnvironment", "Environment", "Ambiente"),
 ("HttpNoneSendAsWritten", "None - send as written", "Nenhum - enviar como está escrito"),
 ("Clear", "Clear", "Limpar"),
 ("HttpSendRequestsExactlyAsTheyAre",
  "Send requests exactly as they are written", "Enviar requisições exatamente como estão escritas"),
 ("HttpEdit", "Edit…", "Editar…"),
 ("HttpAddEnvironmentsAndTheValuesName",
  "Add environments and the values {{name}} resolves from",
  "Adicione ambientes e os valores de onde {{nome}} é resolvido"),
 ("HttpLocalhost5000HealthOrPasteA",
  "localhost:5000/health — or paste a whole curl command here",
  "localhost:5000/health — ou cole um comando curl inteiro aqui"),
 ("HttpACurlCommandPastedHereIs",
  "A curl command pasted here is read into this request: method, URL, headers, cookies and body.",
  "Um comando curl colado aqui é lido para dentro desta requisição: método, URL, cabeçalhos, cookies e corpo."),
 ("HttpMore", "More", "Mais"),
 ("HttpHeaders", "Headers", "Cabeçalhos"),
 ("HttpAdd", "+ Add", "+ Adicionar"),
 ("HttpSendThisHeader", "Send this header", "Enviar este cabeçalho"),
 ("HttpValue", "Value", "Valor"),
 ("HttpBodyJSONIsDetectedFromThe",
  "Body — JSON is detected from the first character",
  "Corpo — JSON é detectado pelo primeiro caractere"),

 ("LogLog", "Log", "Registro"),
 ("LogEverythingTheAppItselfHasDone",
  "Everything the app itself has done this session - not the output of what you ran.",
  "Tudo que o próprio app fez nesta sessão - não a saída do que você executou."),
 ("LogFilter", "Filter", "Filtrar"),
 ("LogProblemsOnly", "Problems only", "Só problemas"),
 ("LogCopyAll", "Copy all", "Copiar tudo"),
 ("LogNothingToReport", "Nothing to report.", "Nada a relatar."),

 ("MainWindowDismiss", "dismiss", "dispensar"),

 ("MemoryMemoryAndProcessor", "Memory and processor", "Memória e processador"),
 ("MemoryMemory", "memory", "memória"),
 ("MemoryProcessor", "processor", "processador"),
 ("MemoryAcrossAllCores", "across all cores", "somando todos os núcleos"),
 ("MemorySampledEveryTwoSeconds", "sampled every two seconds", "medido a cada dois segundos"),
 ("MemoryDisk", "disk", "disco"),
 ("MemoryGpu", "GPU", "GPU"),
 ("MemoryDiskActive", "active time, all disks", "tempo ativo, todos os discos"),
 ("MemoryGpu3d", "3D engine, all processes", "motor 3D, todos os processos"),
 ("MemDiskTotal", "{0} · {1} total", "{0} · {1} no total"),
 ("MemDiskSpace", "{0} used · {1} free", "{0} usados · {1} livres"),
 ("MemNotAvailable", "not available on this machine", "indisponível nesta máquina"),
 ("MemWidgetGpuDisk", "GPU {0} · Disk {1}", "GPU {0} · Disco {1}"),
 ("MemoryLargestProcesses", "Largest processes", "Maiores processos"),
 ("MemoryProcess", "Process", "Processo"),
 ("MemoryShareOfTheHeaviest", "Share of the heaviest", "Fatia do mais pesado"),
 ("MemoryMemory2", "Memory", "Memória"),
 ("MemoryCleanupSteps", "Cleanup steps", "Passos de limpeza"),
 ("MemorySomeTickedStepsNeedAnAdministrator",
  "Some ticked steps need an administrator. Windows will ask once when you clean.",
  "Alguns passos marcados precisam de administrador. O Windows vai pedir uma vez quando você limpar."),
 ("MemoryAdministrator", "administrator", "administrador"),

 ("MemoryWidgetDevDeckMemory", "DevDeck memory", "Memória do DevDeck"),
 ("MemoryWidgetShowDevDeck", "Show DevDeck", "Mostrar o DevDeck"),
 ("MemoryWidgetCleanMemoryNow", "Clean memory now", "Limpar memória agora"),
 ("MemoryWidgetHideWidget", "Hide widget", "Ocultar o widget"),
 ("MemoryWidgetQuitDevDeck", "Quit DevDeck", "Sair do DevDeck"),
 ("MemoryWidgetClickToClean", "click to clean", "clique para limpar"),
 ("MemoryWidgetFreed", "freed", "liberado"),

 ("NamePromptRename", "Rename", "Renomear"),

 ("PaletteRunACommand", "Run a command", "Executar um comando"),
 ("PaletteTypeACommandAPanelOr", "Type a command, a panel or a tool", "Digite um comando, um painel ou uma ferramenta"),
 ("PaletteNothingMatchesThat", "Nothing matches that.", "Nada corresponde a isso."),
 ("PaletteEnterRunsItArrowsMoveEsc",
  "Enter runs it · arrows move · Esc closes",
  "Enter executa · setas movem · Esc fecha"),

 ("ParameterPromptValuesForThisRun", "Values for this run", "Valores para esta execução"),
 ("ParameterPromptThisCommandAsksForValuesBefore",
  "This command asks for values before it runs.",
  "Este comando pede valores antes de executar."),

 ("RunTilePorts", "Listening", "Escutando"),
 ("RunTileContainers", "Containers", "Contêineres"),
 ("RunTileDeck", "Deck", "Deck"),
 ("RunTileHealth", "Health", "Saúde"),
 ("RunContainersUp", "{0} of {1} up", "{0} de {1} ativos"),
 ("RunDeckRunning", "{0} running", "{0} em execução"),
 ("RunHealthUp", "{0} of {1} up", "{0} de {1} no ar"),
 ("RunUnknown", "-", "-"),
 ("RunningFilter", "Filter ports and containers", "Filtrar portas e contêineres"),
 ("RunningAutoRefresh", "Auto-refresh", "Atualizar sozinho"),
 ("RunningAutoRefreshTip", "Re-reads ports, containers and health checks every twelve seconds while this page is open.",
  "Relê portas, contêineres e verificações de saúde a cada doze segundos enquanto esta página está aberta."),
 ("RunningPid", "PID {0}", "PID {0}"),
 ("RunningCopyUrl", "Copy URL", "Copiar URL"),
 ("RunningHealth", "Health checks", "Verificações de saúde"),
 ("RunningHealthBlurb", "URLs asked on every refresh - status code and response time.",
  "URLs consultadas a cada atualização - código de status e tempo de resposta."),
 ("RunningHealthPlaceholder", "http://localhost:5000/health", "http://localhost:5000/health"),
 ("RunningHealthNone", "No checks yet. Add a URL one of your services answers on.",
  "Nenhuma verificação ainda. Adicione uma URL que um dos seus serviços responde."),
 ("RunningCheckNow", "Check now", "Verificar agora"),
 ("RunHealthWaiting", "not checked yet", "ainda não verificado"),
 ("RunHealthMs", "{0} · {1} ms", "{0} · {1} ms"),
 ("RunHealthNoAnswer", "no answer - {0}", "sem resposta - {0}"),
 ("RunHealthBadUrl", "Not a URL: {0}", "Não é uma URL: {0}"),
 ("RunningPortCheck", "Is a port free?", "A porta está livre?"),
 ("RunningPortPlaceholder", "Port, e.g. 5173", "Porta, ex.: 5173"),
 ("RunningCheck", "Check", "Verificar"),
 ("RunPortFree", "{0} is free.", "{0} está livre."),
 ("RunPortTaken", "{0} is taken by {1} (PID {2}).", "{0} está em uso por {1} (PID {2})."),
 ("RunPortTakenUnknown", "{0} is in use.", "{0} está em uso."),
 ("RunPortInvalid", "Type a port between 1 and 65535.", "Digite uma porta entre 1 e 65535."),
 ("RunningDeckActivity", "Running from the deck", "Em execução no deck"),
 ("RunningDeckIdle", "Nothing from the deck is running.", "Nada do deck está em execução."),
 ("RunningTopProcesses", "Heaviest processes", "Processos mais pesados"),
 ("Add", "Add", "Adicionar"),
 ("SettingsEnvVars", "Environment variables", "Variáveis de ambiente"),
 ("SettingsAddVariable", "Add variable", "Adicionar variável"),
 ("SettingsEnvVarsNote",
  "Set on every command the deck runs - read them as $env:name in PowerShell, %name% in batch, $name in a shell. Saved in plain text: keep tokens in Secrets above instead.",
  "Definidas em todo comando que o deck executa - leia como $env:nome no PowerShell, %nome% no batch, $nome no shell. Salvas em texto puro: guarde tokens em Segredos acima."),
 ("CommandsParameters", "Parameters", "Parâmetros"),
 ("CommandsAddParameter", "Add parameter", "Adicionar parâmetro"),
 ("CommandsParameterName", "name", "nome"),
 ("CommandsParameterValue", "value", "valor"),
 ("CommandsParameterOnTip", "Pass this one. Off keeps it in the list without sending it.",
  "Enviar este. Desligado mantém na lista sem enviar."),
 ("CommandsParametersHelp",
  "Passed on every run. PowerShell gets -name \"value\" (use param() in the script); batch gets %1 %2, shell scripts $1 $2. Also in the environment as DEVDECK_ARG_NAME and DEVDECK_ARG_1.",
  "Enviados a cada execução. PowerShell recebe -nome \"valor\" (use param() no script); batch recebe %1 %2, scripts shell $1 $2. Também no ambiente como DEVDECK_ARG_NOME e DEVDECK_ARG_1."),
 ("RunningKill", "End", "Finalizar"),
 ("RunningKillTip", "End this process and everything it started. Unsaved work in it is lost.",
  "Finaliza este processo e tudo o que ele iniciou. Trabalho não salvo nele é perdido."),
 ("RunKilled", "Ended {0} (PID {1}).", "{0} finalizado (PID {1})."),
 ("RunKillFailed", "Could not end {0}: {1}", "Não foi possível finalizar {0}: {1}"),
 ("RunningRunning", "Running", "Em execução"),
 ("RunningOnlyLikelyPorts", "Only likely ports", "Só portas prováveis"),
 ("RunningHidesSystemServicesAndEphemeralPorts",
  "Hides system services and ephemeral ports.",
  "Esconde serviços do sistema e portas efêmeras."),
 ("RunningListening", "Listening", "Escutando"),
 ("RunningOpen", "Open", "Abrir"),
 ("RunningStopWhatHasIt", "Stop what has it", "Parar quem está usando"),
 ("RunningContainers", "Containers", "Contêineres"),
 ("RunningStart", "Start", "Iniciar"),
 ("RunningRestart", "Restart", "Reiniciar"),
 ("RunningLogs", "Logs", "Logs"),
 ("RunningOpenInABrowser", "Open in a browser", "Abrir num navegador"),
 ("RunningFollowItsLogInTheDeck", "Follow its log in the deck", "Acompanhar o log dele no deck"),
 ("RunningNothingBelowUntilAnEngineIs",
  "Nothing below until an engine is installed.",
  "Nada abaixo até que um engine esteja instalado."),
 ("RunningClose", "Close", "Fechar"),

 ("SettingsSettings", "Settings", "Configurações"),
 ("SettingsAppearance", "Appearance", "Aparência"),
 ("SettingsTheme", "Theme", "Tema"),
 ("SettingsFollowTheSystemTracksTheDesktop",
  "Follow the system tracks the desktop. A theme changes the whole window at once - it is a palette the views read live, not a restart. A theme saved by V2 is read back to the nearest match rather than reset.",
  "Seguir o sistema acompanha a área de trabalho. Um tema muda a janela inteira de uma vez - é uma paleta que as telas leem ao vivo, não um reinício. Um tema salvo pelo V2 é lido de volta para o mais próximo em vez de ser descartado."),
 ("SettingsLanguage", "Language", "Idioma"),
 ("SettingsTheLanguageChangesTheMomentYou",
  "The language changes the moment you choose it, without a restart. Each one is written in itself, so it can be found from inside a language you cannot read.",
  "O idioma muda no instante em que você escolhe, sem reiniciar. Cada um está escrito nele mesmo, para poder ser encontrado de dentro de um idioma que você não lê."),
 ("SettingsAi", "AI", "IA"),
 ("SettingsAiNote",
  "The assistant asks whatever is chosen here. A key is kept per provider, so switching between them does not mean typing it again.",
  "O assistente pergunta ao que estiver escolhido aqui. A chave é guardada por provedor, então alternar entre eles não exige digitá-la de novo."),

 ("SettingsBehaviour", "Behaviour", "Comportamento"),
 ("SettingsKeepThisMachineAwakeWhileDevDeck",
  "Keep this machine awake while DevDeck is open",
  "Manter esta máquina acordada enquanto o DevDeck estiver aberto"),
 ("SettingsAlsoStopItLockingItselfAnd",
  "Also stop it locking itself, and stay available in chat apps",
  "Também impedir que ela se bloqueie, e continuar disponível nos apps de conversa"),
 ("SettingsAskBeforePowerActions", "Ask before power actions", "Perguntar antes de ações de energia"),
 ("SettingsFollowOutputAsItArrives", "Follow output as it arrives", "Acompanhar a saída conforme ela chega"),
 ("SettingsKeepAwakeTip",
  "Stops the computer going to sleep while DevDeck is open, so a long build, download or test run is not cut off when you step away. The request is released the moment DevDeck closes.",
  "Impede o computador de dormir enquanto o DevDeck estiver aberto, para que uma compilação, um download ou uma bateria de testes longa não seja interrompida quando você se afastar. O pedido é liberado assim que o DevDeck fecha."),
 ("SettingsStayAvailableTip",
  "Also keeps the screen from locking and your status in Teams or Slack from turning to Away, by pressing a key that does nothing once you have been idle for a minute.",
  "Também impede a tela de bloquear e o seu status no Teams ou no Slack de virar Ausente, pressionando uma tecla que não faz nada depois de você ficar um minuto sem atividade."),
 ("SettingsConfirmPowerTip",
  "Asks for confirmation before DevDeck locks, signs out, restarts or shuts down this machine. This version has no power actions yet; the switch is kept so the choice carried over from V2 is not lost.",
  "Pede confirmação antes de o DevDeck bloquear, sair, reiniciar ou desligar esta máquina. Esta versão ainda não tem ações de energia; a chave é mantida para que a escolha trazida do V2 não se perca."),
 ("SettingsFollowOutputTip",
  "Scrolls a command's output to the newest line as it is written. Turn it off to read earlier output without being pulled back to the bottom.",
  "Rola a saída de um comando até a linha mais nova conforme ela é escrita. Desligue para ler a saída anterior sem ser puxado de volta para o fim."),
 ("SettingsAutoStartTip",
  "Opens DevDeck each time you sign in to this computer, minimised to the taskbar and tray. Only for your user; nobody else who signs in is affected.",
  "Abre o DevDeck toda vez que você entra neste computador, minimizado na barra de tarefas e na bandeja. Só para o seu usuário; ninguém mais que entrar é afetado."),
 ("SettingsNotSleepingAndNotLockingAre",
  "Not sleeping and not locking are two different settings on most machines: a managed Windows install locks on how long since it last saw a keypress, which no power request touches. The second switch holds that clock back with a key nothing maps, and only once you have already been idle for a minute.",
  "Não dormir e não bloquear são duas configurações diferentes na maioria das máquinas: uma instalação gerenciada do Windows bloqueia pelo tempo desde a última tecla, algo que nenhum pedido de energia altera. A segunda chave segura esse relógio com uma tecla que não mapeia nada, e só depois de você já estar um minuto sem atividade."),
 ("SettingsWhenALongCommandFinishes", "When a long command finishes", "Quando um comando longo terminar"),
 ("SettingsTellMeWhenItIsDone", "Tell me when it is done", "Me avise quando terminar"),
 ("SettingsOnlyIfItRanForAt", "Only if it ran for at least", "Só se tiver rodado por pelo menos"),
 ("SettingsSeconds", "seconds", "segundos"),
 ("SettingsADesktopNotificationWhereTheSystem",
  "A desktop notification where the system has one - macOS and Linux. On Windows a real toast needs a registered application identity this app does not claim, so the taskbar button flashes instead.",
  "Uma notificação da área de trabalho onde o sistema tem uma - macOS e Linux. No Windows, uma notificação de verdade exige uma identidade de aplicativo registrada que este app não reivindica, então o botão da barra de tarefas pisca no lugar."),
 ("SettingsSecrets", "Secrets", "Segredos"),
 ("SettingsASecretIsStoredWrappedIn",
  "A secret is stored wrapped, in its own file, and never written into a command or a deck you export. Reference one from any command as {{secret:NAME}} and it is filled in as the command runs.",
  "Um segredo é guardado protegido, num arquivo próprio, e nunca é escrito dentro de um comando ou de um deck que você exporta. Referencie um de qualquer comando como {{secret:NOME}} e ele é preenchido na hora em que o comando roda."),
 ("SettingsValue", "value", "valor"),
 ("SettingsStore", "Store", "Guardar"),
 ("SettingsFromATerminalAndFromA", "From a terminal, and from a link", "De um terminal, e de um link"),
 ("SettingsASavedCommandCanBeStarted",
  'A saved command can be started from outside the window: devdeck run "Build" from a shell, or devdeck://run/Build from a README, a CI page or an editor task. A link can only name a command you have already saved - it cannot supply one to run.',
  'Um comando salvo pode ser iniciado de fora da janela: devdeck run "Build" de um shell, ou devdeck://run/Build de um README, de uma página de CI ou de uma tarefa do editor. Um link só pode citar um comando que você já salvou - ele não pode fornecer um para executar.'),
 ("SettingsWriteTheTerminalLauncher", "Write the terminal launcher", "Criar o atalho de terminal"),
 ("SettingsRemoveIt", "Remove it", "Remover"),
 ("SettingsRegisterDevdeck", "Register devdeck://", "Registrar devdeck://"),
 ("SettingsAKeyThatWorksFromAnywhere", "A key that works from anywhere", "Uma tecla que funciona de qualquer lugar"),
 ("SettingsOneCombinationThatBringsTheDeck",
  "One combination that brings the deck up whatever you are looking at - which is the difference between an app you open and a tool that is simply there.",
  "Uma combinação que traz o deck à frente esteja você olhando o que for - que é a diferença entre um app que você abre e uma ferramenta que simplesmente está ali."),
 ("SettingsClaimIt", "Claim it", "Reservar"),
 ("SettingsGiveItBack", "Give it back", "Devolver"),
 ("SettingsThisBuild", "This build", "Esta versão"),
 ("SettingsSettingsFile", "Settings file", "Arquivo de configurações"),
 ("SettingsUpdates", "Updates", "Atualizações"),
 ("SettingsAsksTheReleasePageWhetherThere",
  "Asks the release page whether there is a newer build. It will not install anything - this app runs commands with your account, and quietly replacing its own executable is the wrong habit for it to have.",
  "Pergunta à página de versões se existe uma compilação mais nova. Não instala nada - este app executa comandos com a sua conta, e trocar o próprio executável em silêncio é o hábito errado para ele ter."),
 ("SettingsCheckNow", "Check now", "Verificar agora"),
 ("SettingsOpenTheReleasePage", "Open the release page", "Abrir a página de versões"),
 ("SetWhatsNew", "What's new", "Novidades"),
 ("SetAutoStart", "Start DevDeck when I sign in", "Iniciar o DevDeck quando eu entrar no sistema"),
 ("SetAutoStartNote",
  "For your user only. It opens minimised, so it is ready on the taskbar and in the tray without getting in front of anything.",
  "Só para o seu usuário. Ele abre minimizado, então fica pronto na barra de tarefas e na bandeja sem ficar na frente de nada."),
 ("SetAutoStartCannot",
  "On macOS a login item belongs to an app bundle. Add DevDeck under System Settings > General > Login Items instead.",
  "No macOS um item de início pertence a um pacote de aplicativo. Adicione o DevDeck em Ajustes do Sistema > Geral > Itens de Início."),
 ("SetAutoStartOn", "DevDeck will start the next time you sign in.", "O DevDeck vai iniciar na próxima vez que você entrar no sistema."),
 ("SetAutoStartOff", "DevDeck will no longer start when you sign in.", "O DevDeck não vai mais iniciar quando você entrar no sistema."),
 ("SetAutoStartFailed", "Could not change the startup entry: {0}", "Não foi possível alterar a entrada de inicialização: {0}"),
 ("NavChangelog", "Changelog", "Novidades"),
 ("NavChangelogBlurb", "What each version brought, and what is waiting in a newer one.", "O que cada versão trouxe, e o que espera numa versão mais nova."),
 ("ChangelogThisVersion", "This version", "Esta versão"),
 ("ChangelogNotInstalled", "Not installed yet", "Ainda não instalada"),
 ("ChangelogCheck", "Check for newer versions", "Procurar versões mais novas"),
 ("ChangelogAsking", "Asking the release page…", "Perguntando à página de versões…"),
 ("ChangelogEmpty", "This build carries no changelog.", "Esta compilação não traz um registro de mudanças."),
 ("ConfirmTitle", "Are you sure?", "Tem certeza?"),
 ("SettingsStartOver", "Start over", "Começar de novo"),
 ("SettingsResetExplains",
  "Throws away every setting, saved command, chain, watch and environment, and puts back the ones the deck ships with. The old file is kept beside the new one with a date on it, so nothing is truly gone.",
  "Descarta todas as configurações, comandos salvos, cadeias, monitores e ambientes, e devolve os que vêm com o aplicativo. O arquivo antigo fica ao lado do novo com uma data no nome, então nada se perde de verdade."),
 ("SettingsResetKeepsSecrets",
  "Stored secrets are left alone. They live in the system keychain rather than in the settings file, and clearing them is a separate button above.",
  "Os segredos guardados não são tocados. Eles ficam no chaveiro do sistema e não no arquivo de configurações, e limpá-los é um botão separado acima."),
 ("SettingsResetNow", "Reset everything", "Redefinir tudo"),
 ("SettingsResetAsk", "Reset every setting to the defaults?", "Redefinir todas as configurações para o padrão?"),
 ("SettingsResetDetail",
  "Your commands, chains, watches, environments and preferences are replaced by the ones the deck ships with. The current file is renamed rather than deleted, and secrets in the keychain are untouched. DevDeck then restarts, which stops anything still running in it.",
  "Seus comandos, cadeias, monitores, ambientes e preferências são substituídos pelos que vêm com o aplicativo. O arquivo atual é renomeado e não apagado, e os segredos no chaveiro não são tocados. Em seguida o DevDeck reinicia, o que interrompe tudo o que ainda estiver rodando nele."),
 ("AutomationDragToResize", "Drag to resize", "Arraste para redimensionar"),
 ("SettingsResetRestarting", "Settings reset. Restarting DevDeck...", "Configurações redefinidas. Reiniciando o DevDeck..."),
 ("SettingsResetDone",
  "Settings reset. Close and open DevDeck to see them - what is on screen is still the old set.",
  "Configurações redefinidas. Feche e abra o DevDeck para vê-las - o que está na tela ainda é o conjunto antigo."),
 ("SettingsResetFailed", "Could not reset: {0}", "Não foi possível redefinir: {0}"),

 ("ToolboxEverythingHereRunsOnThisMachine",
  "Everything here runs on this machine. Nothing you paste is sent anywhere.",
  "Tudo aqui roda nesta máquina. Nada do que você colar é enviado a lugar nenhum."),
 ("ToolboxAgain", "Again", "De novo"),
 ("ToolboxPaste", "Paste", "Colar"),
 ("ToolboxThePattern", "The pattern", "O padrão"),


 # --- Assistant, at runtime ---
 ("AiWhoYou", "You", "Você"),
 ("AiWhoAssistant", "Assistant", "Assistente"),
 ("AiWhoOutput", "Output", "Saída"),
 ("AiWhoProblem", "Problem", "Problema"),
 ("AiReady", "Ready.", "Pronto."),
 ("AiLookingForModel", "Looking for the model…", "Procurando o modelo…"),
 ("AiChecking", "Checking {0}…", "Verificando {0}…"),
 ("AiModelOn", "{0} on {1}", "{0} em {1}"),
 ("AiUsing", "Using {0} from {1}", "Usando {0} de {1}"),
 ("AiSetUpLabel", "Set up {0}", "Instalar {0}"),
 ("AiNothingAnswered", "Nothing answered at {0} — {1}",
  "Nada respondeu em {0} — {1}"),
 ("AiNoEndpoint", "No endpoint.", "Nenhum endpoint."),
 ("AiSettingUp", "Setting up {0}. This can take a few minutes.",
  "Instalando {0}. Isso pode levar alguns minutos."),
 ("AiLocalReady", "{0} is ready.", "{0} está pronto."),
 ("AiNowAsking", "Now asking {0}.", "Perguntando agora para {0}."),
 ("AiAskingEndpoint", "Asking the endpoint what it has…",
  "Perguntando ao endpoint o que ele tem…"),
 ("AiModelsAvailableOne", "{0} model available.", "{0} modelo disponível."),
 ("AiModelsAvailable", "{0} models available.", "{0} modelos disponíveis."),
 ("AiNoModels", "The endpoint answered, but offered no models.",
  "O endpoint respondeu, mas não ofereceu nenhum modelo."),
 ("AiCouldNotReach", "Could not reach {0}: {1}", "Não foi possível alcançar {0}: {1}"),
 ("AiQuestionReady", "The question is ready - the model is not reachable yet.",
  "A pergunta está pronta - o modelo ainda não está acessível."),
 ("AiAsking", "Asking {0}…", "Perguntando para {0}…"),
 ("AiStopped", "Stopped.", "Interrompido."),
 ("AiFailed", "Failed.", "Falhou."),
 ("AiNoAnswer", "_Could not get an answer: {0}_",
  "_Não foi possível obter uma resposta: {0}_"),
 ("AiSavedNewOne", "Saved, and started a new one.", "Salva, e uma nova começada."),
 ("AiCleared", "Cleared.", "Limpa."),
 ("AiAlreadyAttached", "{0} is already attached.", "{0} já está anexado."),
 ("AiAttachedNote", "{0} - sent with your next question.",
  "{0} - enviado com sua próxima pergunta."),
 ("AiCountAttached", "{0} attached.", "{0} anexados."),
 ("AiSentWithOne", "Sent with {0}.", "Enviado com {0}."),
 ("AiSentWithMany", "Sent with {0} files: {1}.", "Enviado com {0} arquivos: {1}."),
 ("AiOpened", "Opened \"{0}\".", "\"{0}\" aberta."),
 ("AiDeleted", "Deleted \"{0}\".", "\"{0}\" apagada."),
 ("AiLoadedPrompt", "Loaded \"{0}\" - fill in the placeholders and send.",
  "\"{0}\" carregado - preencha os espaços e envie."),
 ("AiNothingToSave", "There is nothing in the box to save.",
  "Não há nada na caixa para salvar."),
 ("AiSavedPrompt", "Saved \"{0}\".", "\"{0}\" salvo."),
 ("AiSpendSession", "{0} · {1} USD this session", "{0} · {1} USD nesta sessão"),
 ("AiContextWindow", "{0}% of a {1}k window", "{0}% de uma janela de {1}k"),
 ("AiContextTight", " - clear the chat soon, or it will start forgetting",
  " - limpe a conversa em breve, ou ela vai começar a esquecer"),
 ("AiNoCommandInAnswer", "That answer did not contain a command to save.",
  "Essa resposta não continha um comando para salvar."),
 ("AiNoChainInAnswer", "There is no chain in this answer to create.", "Não há nenhuma sequência nesta resposta para criar."),
 ("AiChainCreated",
  "Created the chain \"{0}\" with {1} steps, and added {2} new commands to the deck.",
  "Criada a sequência \"{0}\" com {1} passos, e adicionados {2} comandos novos ao deck."),
 ("AiChainMissing",
  "These steps name commands the deck does not have: {0}. The chain marks them until you add them.",
  "Estes passos citam comandos que o deck não tem: {0}. A sequência os marca até você adicioná-los."),
 ("AiChainRenamed",
  "\"{0}\" is already in the deck with a different script, so this one was added as \"{1}\".",
  "\"{0}\" já está no deck com outro script, então este foi adicionado como \"{1}\"."),
 ("AiConfirmBlurb",
  "This was written by a model. It runs in {0} with your account - read it first.",
  "Isto foi escrito por um modelo. Roda em {0} com a sua conta - leia antes."),
 ("AiNotRun", "Not run.", "Não executado."),
 ("AiNeedWorkspace", "Choose a workspace folder first - that is where it would run.",
  "Escolha uma pasta de trabalho primeiro - é onde isso rodaria."),
 ("AiExitCode", "— exit code {0} after {1}", "— código de saída {0} depois de {1}"),
 ("AiRanCleanly", "That ran cleanly.", "Rodou sem erros."),
 ("AiExited", "That exited {0}.", "Saiu com {0}."),
 ("AiStoppedAfter", "— stopped after {0}", "— interrompido depois de {0}"),
 ("AiCouldNotRunLine", "— could not run it: {0}",
  "— não foi possível executar: {0}"),
 ("AiCouldNotRun", "Could not run it: {0}", "Não foi possível executar: {0}"),
 ("ToolCopied", "Copied.", "Copiado."),
 ("ToolGroupData", "Data", "Dados"),
 ("ToolGroupEncoding", "Encoding", "Codificação"),
 ("ToolGroupInspect", "Inspect", "Inspecionar"),
 ("ToolGroupGenerate", "Generate", "Gerar"),
 ("ToolGroupText", "Text", "Texto"),
 ("ToolJsonFormat", "JSON: format", "JSON: formatar"),
 ("ToolJsonFormatHint", "Comments and trailing commas are accepted.",
  "Comentários e vírgulas sobrando são aceitos."),
 ("ToolJsonMinify", "JSON: minify", "JSON: minificar"),
 ("ToolXmlFormat", "XML: format", "XML: formatar"),
 ("ToolBase64Encode", "Base64: encode", "Base64: codificar"),
 ("ToolBase64Decode", "Base64: decode", "Base64: decodificar"),
 ("ToolBase64DecodeHint", "URL-safe input and missing padding are both fine.",
  "Entrada segura para URL e preenchimento faltando funcionam do mesmo jeito."),
 ("ToolUrlEncode", "URL: encode", "URL: codificar"),
 ("ToolUrlDecode", "URL: decode", "URL: decodificar"),
 ("ToolHtmlEscape", "HTML: escape", "HTML: escapar"),
 ("ToolHtmlUnescape", "HTML: unescape", "HTML: desescapar"),
 ("ToolJwtDecode", "JWT: decode", "JWT: decodificar"),
 ("ToolJwtDecodeHint", "The signature is not checked. Decoding a token does not verify it.",
  "A assinatura não é conferida. Decodificar um token não o verifica."),
 ("ToolHash", "Hash", "Hash"),
 ("ToolTimestamp", "Timestamp", "Data e hora"),
 ("ToolTimestampHint", "Unix seconds or milliseconds, or a date to turn into one.",
  "Segundos ou milissegundos Unix, ou uma data para virar um deles."),
 ("ToolUuid", "UUID", "UUID"),
 ("ToolUuidHint", "A new one each time you press Again.",
  "Um novo a cada vez que você aperta De novo."),
 ("ToolCase", "Case", "Maiúsculas e minúsculas"),
 ("ToolSortLines", "Sort lines", "Ordenar linhas"),
 ("ToolSortLinesHint", "Sorted and de-duplicated.", "Ordenadas e sem repetições."),
 ("ToolRegex", "Regex", "Expressão regular"),
 ("ToolRegexHint", "The pattern goes above, the text to search below.",
  "O padrão vai em cima, o texto a procurar embaixo."),
 ("ToolboxPasteHere", "Paste here", "Cole aqui"),
 ("ToolboxUseAsInput", "Use as input", "Usar como entrada"),
]

# Once the views are converted they no longer carry the English text, so the check that used to
# compare the table against the views now checks the other invariant that matters: a key asked
# for anywhere has to be in the table, and a key in the table should be asked for somewhere.
#
# Both halves are scanned - the views for {loc:T Key} and the C# for Strings.Text/Format("Key") -
# because plenty of what the app says is built in a view model and never appears in any view.
import os, re

table = {k: en for k, en, _ in T}
assert len(table) == len(T), "duplicate key in the table"

used = set()

# This file's own output is skipped. Strings.cs and Tr.cs carry every key by construction, so
# counting them as usage would mean a key could never be retired: it would always look asked for
# by the very files about to be rewritten without it.
generated = {"Strings.cs", "Tr.cs"}

for root, _, names in os.walk("src"):
    for name in names:
        if name.endswith((".axaml", ".cs")) and name not in generated:
            body = open(os.path.join(root, name), encoding="utf-8-sig", errors="replace").read()
            used.update(re.findall(r"\{loc:T (\w+)\}", body))
            used.update(re.findall(r'Strings\.(?:Text|Format)\(\s*"(\w+)"', body))
            # A key chosen at the call site - `count == 1 ? "AOne" : "AMany"` - so the name is
            # never next to Strings.Format. Any bare string that is also a key in the table is
            # counted as used: a false positive here is harmless, a false orphan is noise.
            used.update(k for k in re.findall(r'"(\w+)"', body) if k in table)

missing = sorted(used - set(table))
assert not missing, f"asked for, not translated: {missing}"

# A warning rather than a failure: a key is written here first and wired up next, so an orphan
# is the normal state for the minute in between.
orphan = sorted(set(table) - used)
if orphan:
    print("  not used anywhere yet:", ", ".join(orphan))

def cs(text):
    return '"' + text.replace("\\", "\\\\").replace('"', '\\"') + '"'


lines = []
for k, en, pt in T:
    lines.append(f"                [{cs(k)}] = {cs(en)},")

pt_lines = []
for k, en, pt in T:
    pt_lines.append(f"                [{cs(k)}] = {cs(pt)},")

body = """using System.Globalization;

namespace DevDeck.Core
{
    /// <summary>
    ///  Every word the app says, in each language it says it in.
    /// </summary>
    /// <remarks>
    ///  A dictionary per language rather than .resx and satellite assemblies. Two reasons, and the
    ///  second is the one that decided it: the app ships as a single self-contained folder per
    ///  platform, and satellite assemblies are one more thing that has to arrive intact for the UI
    ///  to come up at all - a missing one is an exception at load rather than a word in English.
    ///  And a table that is a plain C# file can be read, diffed and reviewed beside the change that
    ///  added a string to a view.
    ///
    ///  English is the fallback for a key that a translation has not caught up with, and
    ///  <see cref="Text"/> returns the key itself if English has not got it either. That is
    ///  deliberate: a screen showing HttpNoFlag is obviously a bug in this file, where a screen
    ///  showing an empty label is a bug somewhere nobody can see.
    /// </remarks>
    internal static class Strings
    {
        /// <summary>
        ///  The table in force. Null until a language is chosen, which reads as English.
        /// </summary>
        /// <remarks>
        ///  Nullable rather than initialised to English, because static field initialisers run in
        ///  declaration order and the tables are declared below this: reading English here would
        ///  read it before it exists.
        /// </remarks>
        private static IReadOnlyDictionary<string, string>? current;

        /// <summary>The language the app is currently reading in.</summary>
        public static LanguageChoice Language { get; private set; } = AppLanguage.English;

        /// <summary>Raised when the language changes, so the views can re-read every string.</summary>
        public static event Action? Changed;

        /// <summary>
        ///  Switches language, resolving "follow the system" first.
        /// </summary>
        public static void Use(LanguageChoice language)
        {
            LanguageChoice wanted = AppLanguage.Resolve(language);

            Language = wanted;
            current = wanted.Id == AppLanguage.Portuguese.Id ? Portuguese : English;

            Changed?.Invoke();
        }

        /// <summary>
        ///  A string with values written into it: "Imported {0} commands".
        /// </summary>
        /// <remarks>
        ///  Numbered holes rather than interpolation, because a translation is allowed to put them
        ///  in a different order - which is the whole reason a status line cannot be built by
        ///  gluing translated fragments together.
        ///
        ///  Formatted in the invariant culture deliberately. The holes carry names, counts and
        ///  exception messages rather than money or dates that need local conventions, and the app
        ///  language is a separate choice from the machine's regional format.
        /// </remarks>
        public static string Format(string key, params object?[] values) =>
            string.Format(CultureInfo.InvariantCulture, Text(key), values);

        /// <summary>
        ///  What to show for a key, falling back to English and then to the key itself.
        /// </summary>
        public static string Text(string key)
        {
            if ((current ?? English).TryGetValue(key, out string? found))
            {
                return found;
            }

            return English.TryGetValue(key, out string? fallback) ? fallback : key;
        }

        /// <summary>
        ///  Every key the app has a word for.
        /// </summary>
        /// <remarks>
        ///  Here for the tests, which walk the whole table rather than naming strings one at a
        ///  time: a translation with a format hole the English has not got is a FormatException the
        ///  first time someone reaches that line, and the only way to catch that before they do is
        ///  to check all of them.
        /// </remarks>
        public static IEnumerable<string> Keys => English.Keys;

        private static readonly Dictionary<string, string> English =
            new(StringComparer.Ordinal)
            {
__EN__
            };

        private static readonly Dictionary<string, string> Portuguese =
            new(StringComparer.Ordinal)
            {
__PT__
            };
    }
}
"""

body = body.replace("__EN__", "\n".join(lines)).replace("__PT__", "\n".join(pt_lines))

open("src/DevDeck.Core/Settings/Strings.cs", "w", encoding="utf-8").write(body)
print("wrote Strings.cs:", len(T), "keys")


# ---------------------------------------------------------------------------
# Tr: one property per key, so a view binds to a plain path.
# ---------------------------------------------------------------------------
props = []
for k, en, pt in T:
    props.append(f"        /// <summary>{en[:70].replace('&', '&amp;').replace('<', '&lt;')}</summary>")
    props.append(f"        public string {k} => Strings.Text({cs(k)});")
    props.append("")

names = ",\n".join(f"            {cs(k)}" for k, _, _ in T)

tr = """using System.ComponentModel;
using DevDeck.Core;

namespace DevDeck.App.Localisation
{
    /// <summary>
    ///  What the views bind to for every word on screen.
    /// </summary>
    /// <remarks>
    ///  Generated, one property per string, rather than an indexer. A property is a plain binding
    ///  path, which is the one shape every binding engine handles the same way - an indexer path
    ///  depends on the accessor plugin resolving a string key through reflection, and a binding
    ///  that silently resolves to nothing presents as a blank label rather than as an error.
    ///
    ///  A single instance, held by <see cref="Current"/>, because the language is a property of the
    ///  application and not of any one panel. Changing it raises PropertyChanged for every string,
    ///  which is how the whole window re-reads itself without being rebuilt: the alternative is
    ///  asking the user to restart, and a setting that needs a restart is a setting people do not
    ///  believe worked.
    /// </remarks>
    public sealed class Tr : INotifyPropertyChanged
    {
        /// <summary>The instance every view binds against.</summary>
        public static Tr Current { get; } = new();

        private Tr()
        {
            Strings.Changed += Reread;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        ///  Tells every binding to read its string again.
        /// </summary>
        /// <remarks>
        ///  Named one at a time rather than with the empty name that conventionally means "all of
        ///  them". Both are meant to work; only this one is worth relying on, and the cost is a few
        ///  hundred notifications on an action the user takes once.
        /// </remarks>
        private void Reread()
        {
            foreach (string name in Keys)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private static readonly string[] Keys =
        [
__NAMES__,
        ];

__PROPS__    }
}
"""

tr = tr.replace("__NAMES__", names).replace("__PROPS__", "\n".join(props))
open("src/DevDeck.App/Localisation/Tr.cs", "w", encoding="utf-8").write(tr)
print("wrote Tr.cs")
