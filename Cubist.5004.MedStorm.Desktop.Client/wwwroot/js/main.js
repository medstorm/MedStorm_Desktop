var app = (function () {

	let startTimestamp;
	let lastTimestamp = 0;
	let timer, interval;
	let isFirst = true;
	let fullDataSet = {};
	let graphData = {};
	let skinCondGraphData = [];
	let badSignalData = [];
	let patientId;

	let lines = {};
	let areas = {};
	let x = {};
	let y = {};

	let isConnected = false;
	let debug = false;

	let isDefault = { x: true, y: true };
	let selectedApplication = 'anaesthesia';
	let skinCondDataDomain = 15;
	let xDomains = [60, 45, 30, 15];
	let yDomainValue = { pps: 10, area: 100, nerveblock: 10 };
	let acceptedIntervals = { pps: { max: 0.4, min: 0.1 }, area: { max: 0.4, min: 0.0 }, nerveblock: { max: 0.1, min: 0.0 } };
	let textLabels = { pps: 'Pain-Nociceptive', area: 'Awakening', nerveblock: 'Nerve Block' };
	let applications = { anaesthesia: ['pps', 'area', 'nerveblock'], postOperative: ['pps'], icu: ['pps', 'area'], infants: ['pps'], withdrawal: ['pps'], neuralBlock: ['nerveblock'] };

	let connection = new signalR.HubConnectionBuilder().withUrl("/bleHub").build();
	let exportDataConnection = new signalR.HubConnectionBuilder().withUrl("/dataExporthub").build();

	window.addEventListener('beforeunload', (e) => {
		sessionStorage.setItem('pageUnloaded', true);
		connection.invoke("AskServerToClose").catch(function (err) {
			return console.error(err.toString());
		});
	});

	function toggleFullScreen() {
		if (!document.fullscreenElement) {
			document.documentElement.requestFullscreen();
		} else {
			if (document.exitFullscreen) {
				document.exitFullscreen();
			}
		}
	}

	document.addEventListener("dblclick", function () {
		toggleFullScreen();
	}, false);

	function checkIfReloaded() {
		if (sessionStorage.getItem('pageUnloaded')) {
			connection.invoke("PageReloaded").catch(function (err) {
				return console.error(err.toString());
			});
		}
	}

	connection.on("SendConnectionStatus", (status) => {
		isConnected = status;
		toggleConnectDisconnect();

		if (status && exportDataConnection.state == 'Disconnected') {
			exportDataConnection.start();
		}
	});

    connection.on("MonitorConnectionResult", function(connectionSuccessful) {
        toggleConnectToMonitorButton(connectionSuccessful);
    });

	connection.on("SendMessage", async function (message) {
		let incomingObject = parseData(message);

		// Messages received within 800ms of each other will create squiggles in the graph.
		if (incomingObject.timestamp - lastTimestamp < 800) {
			incomingObject.timestamp = lastTimestamp + 800;
			console.log("Adjusting message timestamp.")
		}
		addDataObject(incomingObject);
		lastTimestamp = incomingObject.timestamp;

		if (isFirst) {
			d3.select('#monitor').selectAll("*").remove();
			d3.select('#skinCondDataMonitor').selectAll("*").remove();

			createMonitor();
			createSkinCondDataMonitor();
			startTimer();

			isFirst = false;
		}
	});

	connection.on("ClosingApplication", function () {
		connection.stop().then(() => {
			openModal('modalCloseApplication');
			resetApplication();
		});
	});

	connection.on("DeviceDisconnected", function () {
		isConnected = false;
		toggleConnectDisconnect();

		resetApplication();
	});

	connection.on("ReconnectDevice", function () {
		isConnected = true;
		toggleConnectDisconnect();
	});

	connection.on("CheckIfDebug", function (message) {
		debug = message;
	});

	exportDataConnection.on("AlertFilePath", (filePath) => {
		alert(`Data stored at ${filePath}`);
	});

	function startMonitor() {
		console.log("Invoke StartAdvertising");

		connection.invoke("StartAdvertising").catch(function (err) {
			return console.error(err.toString());
		}).then(() => {
			isConnected = true;
			toggleConnectDisconnect();
			exportDataConnection.start();
		});

		event.preventDefault();
	}

	function changeApplication(application) {
		selectedApplication = application.value;

		if (!isFirst) {
			graphData = {};

			for (let i = 0; i < applications[selectedApplication].length; i++) {
				let currentId = applications[selectedApplication][i];
				graphData[currentId] = fullDataSet[currentId].slice();
			}

			d3.select('#monitor').selectAll("*").remove();

			createMonitor();
		}
	}

	function parseData(message) {
		let msgSplit = message.split(' ');
		let messageId = msgSplit[0].toLowerCase();  //working value, eg 'PPS'
		let timestamp = Date.now();

		let newDataObject = {
			timestamp: null,
			properties: []
		};

		if (messageId == "combined") {
			let stringValue = msgSplit[1];
			let stringValueArray = stringValue.split('|');
			let properties = [];
			
			stringValueArray.forEach(prop =>
			{
				let propertyStringValueArray = prop.split(':');
				let id = propertyStringValueArray[0].toLowerCase();
				let value = id != "skincond" ? parseFloat(propertyStringValueArray[1]) : JSON.parse(propertyStringValueArray[1]);

				if (id == 'timestamp') {
					newDataObject.timestamp = value;
					return;
				}

				if (id == 'badsignal') {
					let isBadSignal = value != 0;
					if (isBadSignal) {
						blinkIndexNumbers();
						badSignalData.push({ timestamp: timestamp });

						while (badSignalData.length > 120) {
							badSignalData.shift();
						}
					}
					updateSQClass(isBadSignal);
					
					return;
				}

				let property = {
					id: id,
					value: value
				};

				properties.push(property);
			});

			newDataObject.properties = properties;
		}

		if (debug) {
			console.log(newDataObject);
		}

		return newDataObject;
	}

	function addDataObject(incomingObject) {
		let n = 170;

		if (isFirst) {
			startTimestamp = Date.now();

			incomingObject.properties.forEach(prop => {
				if (prop.id != 'skincond') {
					if (applications[selectedApplication].includes(prop.id)) {
						graphData[prop.id] = [];
						graphData[prop.id].push({ timestamp: incomingObject.timestamp, value: prop.value });
					}

					fullDataSet[prop.id] = [];
					fullDataSet[prop.id].push({ timestamp: incomingObject.timestamp, value: prop.value });
				} else {
					addSkinCondData(prop.value, incomingObject.timestamp);
				}
			});
		} else {
			incomingObject.properties.forEach(async prop => {
				if (prop.id != 'skincond') {
					if (applications[selectedApplication].includes(prop.id)) {
						d3.select(`#text-${prop.id}`).data([prop.value]).text((d) => { return d; });
						graphData[prop.id].push({ timestamp: incomingObject.timestamp, value: prop.value });
						while (graphData[prop.id].length > n) {
							graphData[prop.id].shift();
						}
					}

					fullDataSet[prop.id].push({ timestamp: incomingObject.timestamp, value: prop.value });
					while (fullDataSet[prop.id].length > n) {
						fullDataSet[prop.id].shift();
					}
					
				} else {
					addSkinCondData(prop.value, incomingObject.timestamp, n);
				}
			});
		}

		autoZoom();
	}

	function addSkinCondData(skinCondArray, timestampIncomingObject, n) {
		let diffInMs = 0.2 * 1000;
		for (let i = 0; i < skinCondArray.length; i++) {
			let timestamp = timestampIncomingObject - (skinCondArray.length - 1 - i) * diffInMs;
			skinCondGraphData.push({ timestamp: timestamp, value: skinCondArray[i] });
			while (skinCondGraphData.length > n) {
				skinCondGraphData.shift();
			}
		}
	}

	function createMonitor() {
		let monitor = d3.select('#monitor');

		for (let prop in graphData) {
			let newMonitor = monitor.append('div')
				.attr('class', 'display')
				.attr('id', `display-${prop}`);

			newMonitor.append('div')
				.attr('class', 'displayText')
				.attr('id', `displayText-${prop}`)
				.append('div')
				.attr('class', 'currentValue')
				.attr('id', `currentValue-${prop}`);

			newMonitor.append('div')
				.attr('class', 'lineGraph')
				.attr('id', `lineGraph-${prop}`);

			createZoomButtons(prop);
			createGraph(prop);
			createTextBox(prop, graphData[prop][0].value);
		}
	}

	function createSkinCondDataMonitor() {
		let monitor = d3.select('#skinCondDataMonitor');

		monitor.append('div')
			.attr('class', 'lineGraph')
			.attr('id', 'lineGraph-skinCond');

		createSkinCondDataGraph();
	}

	function createSkinCondDataGraph() {
		let id = 'skinCond';

		let screenHeight = $(window).height();
		let width = 269;
		let height = screenHeight - 420;
		let margin = { top: 50, right: 1, bottom: 20, left: 30 };

		x[id] = d3.scaleLinear()
			.range([0, width]);

		x[id].domainIndex = 0;

		y[id] = d3.scaleLinear()
			.domain([10.0, 15.0])
			.range([height, 0]);

		lines[id] = d3.line()
			.x((d) => { return x[id](d.timestamp); })
			.y((d) => { return y[id](d.value); })
			.curve(d3.curveMonotoneX);

		let svg = d3.select(`#lineGraph-${id}`)
			.append('svg')
			.attr('class', 'svg')
			.attr('width', width + margin.left + margin.right)
			.attr('height', height + margin.top + margin.bottom);

		let g = svg.append('g')
			.attr('transform', `translate(${margin.left},${margin.top})`);

		g.append('defs').append('clipPath')
			.attr('id', `clip-${id}`)
			.append('rect')
			.attr('width', width)
			.attr('height', height);

		g.append('g')
			.attr('clip-path', `url(#clip-${id})`)
			.append('path')
			.datum(skinCondGraphData)
			.attr('class', 'line')
			.attr('id', `path-${id}`)
			.attr('stroke-width', '1');

		let xGrid = g.append("g")
			.attr("class", "grid xGrid")
			.attr('id', `${id}Axis`)
			.attr("transform", "translate(0," + y[id](10) + ")")
			.call(x[id].gridlines = make_x_gridlines(id).tickSize(-height).tickFormat(""));

		g.append("g")
			.attr("class", "grid yGrid")
			.call(y[id].gridlines = make_y_gridlines(y[id])
				.tickSize(-width)
				.tickFormat("")
			);

		let xAxis = g.append('g')
			.attr('class', 'axis xAxis')
			.attr('id', `${id}Axis`)
			.attr("transform", "translate(0," + y[id](10) + ")")
			.call(x[id].axis = d3.axisBottom(x[id]).ticks(4).tickFormat(d3.timeFormat("%H:%M:%S")));

		let yAxis = g.append('g')
			.attr('class', 'axis axis-y')
			.attr('id', `${id}Axis`)
			.call(y[id].axis = d3.axisLeft(y[id]).ticks(5));

		svg.append("text")
			.attr("x", 30)
			.attr("y", 40)
			.attr('fill', '#c5e3ee')
			.attr('font-size', '12px')
			.text("MicroSiemens");
	}

	function createZoomButtons(id) {
		let zoomInButton = d3.select(`#lineGraph-${id}`)
			.append("button")
			.attr("name", "zoomIn")
			.attr('class', 'zoomButton')
			.on("click", () => { return zoomIn(id); })
			.append('i')
			.attr('class', 'fa fa-plus');

		let zoomOutButton = d3.select(`#lineGraph-${id}`)
			.append("button")
			.attr("name", "zoomOut")
			.attr('class', 'zoomButton')
			.on("click", () => { return zoomOut(id); })
			.append('i')
			.attr('class', 'fa fa-minus');
	}

	function createGraph(id) {
		let screenHeight = $(window).height() - 25;
		let parentWidth = $(`#lineGraph-${id}`).width();
		let margin = { top: 20, right: 20, bottom: 20, left: 40 };
		let factor = 1 / Object.keys(graphData).length;

		let width = parentWidth - (margin.left + margin.right) - 50;
		let height = (factor - 0.03) * screenHeight - (margin.top + margin.bottom);
		if (factor == 1) {
			height = (factor - 0.05) * screenHeight - (margin.top + margin.bottom);
		}

		x[id] = d3.scaleLinear()
			.range([0, width]);

		x[id].domainIndex = 0;

		y[id] = d3.scaleLinear()
			.domain([0, yDomainValue[id]])
			.range([height, 0]);

		lines[id] = d3.line()
			.x((d) => { return x[id](d.timestamp); })
			.y((d) => { return y[id](d.value); })
			.curve(d3.curveMonotoneX);

		areas[id] = d3.area()
			.x(function (d) { return x[id](d.timestamp); })
			.y1((d) => { return y[id](d.value); })
			.y0(function () { return height; })
			.curve(d3.curveMonotoneX);

		let svg = d3.select(`#lineGraph-${id}`)
			.append('svg')
			.attr('class', 'svg')
			.attr('width', width + margin.left + margin.right)
			.attr('height', height + margin.top + margin.bottom);

		let g = svg.append('g')
			.attr('transform', `translate(${margin.left},${margin.top})`);

		addPath(g, id, width, height);
		addRect(g, id, width, height);
		addBackground(g, id, width, height);
		addLines(g, id, width, height);
		addGrid(g, id, width, height);
		addAxes(g, id);
	}

	function updateBadSignalLines(id) {
		let background = d3.select(`#background-${id}`);
		let height = $(`#backgroundRect-${id}`).height();

		let lines = background.selectAll('.badSignalLine')
			.data(badSignalData, (d) => { return d.timestamp; });

		lines.enter()
		  .append('line')
			.attr('class', 'badSignalLine')
			.attr('y1', 0)
			.attr('y2', height)
		  .merge(lines)
			.attr('x1', (d) => { return x[id](d.timestamp); })
			.attr('x2', (d) => { return x[id](d.timestamp); });

		lines.exit().remove();
	}

	function addBackground(g, id, width, height) {
		g.append('text')
			.attr('id', 'badSignalText')
			.attr("x", width / 2)
			.attr("y", -5)
			.text("Bad signal Quality");

		g.append('defs').append('clipPath')
			.attr('id', `backgroundPath-${id}`)
		  .append('rect')
			.attr('id', `backgroundRect-${id}`)
			.attr('width', width)
			.attr('height', height);

		g.append('g').attr('clip-path', `url(#backgroundPath-${id})`)
			.append('g')
			.attr('id', `background-${id}`);
	}

	function addPath(g, id, width, height) {
		g.append('defs').append('clipPath')
			.attr('id', `clip-${id}`)
			.append('rect')
			.attr('width', width)
			.attr('height', height);

		g.append('g')
			.attr('clip-path', `url(#clip-${id})`)
			.append('path')
			.datum(graphData[id])
			.attr('class', 'line')
			.attr('id', `path-${id}`);

		g.append('g')
			.attr('clip-path', `url(#clip-${id})`)
			.append('path')
			.datum(graphData[id])
			.attr('class', 'area')
			.attr('id', `area-${id}`)
			.attr('d', areas[id]);
	}

	function addRect(g, id, width, height) {
		let y = height - acceptedIntervals[id].max * height;
		let rectHeight = (acceptedIntervals[id].max - acceptedIntervals[id].min) * height;
		
		if (id == 'pps') {
			y = y + 1;
			rectHeight = rectHeight - 1;
		}

		g.append("rect")
			.attr("x", 0)
			.attr("y", y)
			.attr("width", width)
			.attr("height", rectHeight)
			.attr('fill', '#055d7a')
			.attr('opacity', 0.5);
	}

	function addLines(g, id, width, height) {
		let y = height - acceptedIntervals[id].max * height;
		y = id == 'pps' ? y + 1 : y + 0.5;

		g.append("line")
			.attr("x1", 0)
			.attr("y1", y)
			.attr("x2", width)
			.attr("y2", y)
			.attr('stroke', '#c5e3ee')
			.attr('stroke-width', '1px');

		if (id == 'pps') {
			g.append("line")
				.attr("x1", 0)
				.attr("y1", (height - acceptedIntervals[id].min * height) + 0.5)
				.attr("x2", width)
				.attr("y2", (height - acceptedIntervals[id].min * height) + 0.5)
				.attr('stroke', '#c5e3ee')
				.attr('stroke-width', '1px');
		}
	}

	function addGrid(g, id, width, height) {
		let xGrid = g.append("g")
			.attr("class", "grid xGrid")
			.attr('id', `${id}Axis`)
			.attr("transform", "translate(0," + y[id](0) + ")")
			.call(x[id].gridlines = make_x_gridlines(id).tickSize(-height).tickFormat(""));

		let yGrid = g.append("g")
			.attr("class", "grid")
			.call(y[id].gridlines = make_y_gridlines(y[id])
				.tickSize(-width)
				.tickFormat("")
			);
	}

	function addAxes(g, id) {
		let xAxis = g.append('g')
			.attr('class', 'axis xAxis')
			.attr('id', `${id}Axis`)
			.attr("transform", "translate(0," + y[id](0) + ")")
			.call(x[id].axis = d3.axisBottom(x[id]).ticks(3).tickFormat(d3.timeFormat("%H:%M:%S")));

		let yAxis = g.append('g')
			.attr('class', 'axis axis-y')
			.attr('id', `${id}Axis`)
			.call(y[id].axis = d3.axisLeft(y[id]).ticks(5));

		if (id == 'pps') {
			yAxis.call(y[id].axis = d3.axisLeft(y[id]).tickValues([0, 1, 4, 6, 8, 10]));
		}
	}

	function createTextBox(id, currentValue) {
		let svg = d3.select(`#currentValue-${id}`)
			.append('svg')
			.attr('class', 'svg svgTextBox')
			.attr('height', '100%')
			.attr('width', '100%');

		let g = svg.append('g')
			.attr('transform', 'translate(' + 71 + ',' + 85 + ')');

		this.text = g.selectAll('text')
			.data([currentValue]).enter()
			.append('text')
			.attr('fill', 'white')
			.attr('class', 'text indexNumber')
			.attr('id', `text-${id}`)
			.text((d) => { return d; });

		let textLabel = g.append('text');

		textLabel
			.attr('class', 'textLabel')
			.attr('id', `${id}TextLabel`)
			.attr('y', -64)
			.text(function (d) {
				if (selectedApplication == 'withdrawal') {
					return 'Withdrawal';
				}
				return textLabels[id];
			});
	}

	function startTimer() {
		timer = d3.timer(() => {
			for (let prop in graphData) {
				if (applications[selectedApplication].includes(prop)) {
					updateXAxis(prop);
					updatePath(prop);
					updateArea(prop);
					updateBadSignalLines(prop);
				}
			}

			skinCondDataGraphUpdate();
		});
	}

	function updateXAxis(prop) {
		let now = Date.now();
		let xDomainIndex = x[prop].domainIndex;
		let xRange = xDomains[xDomainIndex];
		let monitor = d3.select('#monitor');
 
		if (now > startTimestamp + xRange * 1000) {
			x[prop].domain([(now - xRange * 1000), (now - 1000)]);
		} else {
			x[prop].domain([(startTimestamp), (startTimestamp + (xRange - 1) * 1000)]);
		}

		monitor.select(`.xAxis#${prop}Axis`).call(x[prop].axis);
		monitor.select(`.xGrid#${prop}Axis`).call(x[prop].gridlines);
	}

	function updatePath(prop) {
		let path = d3.select(`#path-${prop}`);
		path.attr('d', lines[prop]);
	}

	function updateArea(prop) {
		let area = d3.select(`#area-${prop}`);
		area.attr('d', areas[prop]);
	}

	function updateSQClass(isBadSignal) {
		$('#monitor').toggleClass('badSignal', isBadSignal);
	}

	function make_x_gridlines(id) {
		return d3.axisBottom(x[id]).ticks(4);
	}

	function make_y_gridlines(y) {
		return d3.axisLeft(y).ticks(5);
	}

	function zoomIn(id) {
		let monitor = d3.select('#monitor');
		let xDomainIndex = x[id].domainIndex;

		if (xDomainIndex != xDomains.length - 1) {
			x[id].domainIndex = xDomainIndex + 1;
			let xAxisSeconds = xDomains[x[id].domainIndex];
			let now = Date.now();
			x[id].domain([(now - xAxisSeconds * 1000), (now - 1000)]);

			updateGridAndAxis(monitor, id);
		}
	}

	function zoomOut(id) {
		let monitor = d3.select('#monitor');
		let xDomainIndex = x[id].domainIndex;

		if (xDomainIndex != 0) {
			x[id].domainIndex = xDomainIndex - 1;
			let xRange = xDomains[x[id].domainIndex];
			let now = Date.now();
			x[id].domain([(now - xRange * 1000), (now - 1000)]);

			updateGridAndAxis(monitor, id);
		}
	}

	function skinCondDataGraphChangeZoom(seconds) {
		let monitor = d3.select('#skinCondDataMonitor');
		let id = 'skinCond';
		let now = Date.now();
		skinCondDataDomain = seconds;

		x[id].domain([(now - skinCondDataDomain * 1000), (now - 1000)]);

		updateGridAndAxis(monitor, id);

		if (skinCondDataDomain != 15) {
			isDefault.x = false;
			d3.select(`#zoomOutX`).classed("clicked", true);
		} else {
			isDefault.x = true;
			d3.selectAll(`#zoomOutX`).classed("clicked", false);
		}

		d3.select('#zoomSC').classed("addBorder", (!isDefault.x || !isDefault.y));
	}

	function toggleZoom() {
		let zoomButtons = d3.selectAll('.zoomSkinCond').classed("hideZoomButtons", !d3.selectAll('.zoomSkinCond').classed("hideZoomButtons"));

		$('#testIcon').toggleClass("fa-angle-down fa-angle-right")
	}

	function autoZoom() {
		let id = 'skinCond';
		let currentlyDrawnData = getCurrentlyDrawnData(skinCondGraphData, id);

		let max = Math.max.apply(Math, currentlyDrawnData.map(function (o) { return o.value; }));
		let min = Math.min.apply(Math, currentlyDrawnData.map(function (o) { return o.value; }));

		y[id].domain([min - 0.2, max + 0.2]);
		d3.select('#skinCondDataMonitor').select(`.axis-y#${id}Axis`).call(y[id].axis);
		d3.select('#skinCondDataMonitor').selectAll('.yGrid').call(y[id].gridlines);
	}

	function getCurrentlyDrawnData(data, id) {
		let currentlyDrawnData = data.filter(object => {
			return object.timestamp > x[id].domain()[0];
		});

		return currentlyDrawnData;
	}

	function skinCondDataGraphUpdate() {
		let monitor = d3.select('#skinCondDataMonitor');
		let id = 'skinCond';
		let now = Date.now();

		if (now > startTimestamp + skinCondDataDomain * 1000) {
			x[id].domain([(now - skinCondDataDomain * 1000), (now - 1000)]);
		} else {
			x[id].domain([(startTimestamp), (startTimestamp + (skinCondDataDomain - 1) * 1000)]);
		}

		updateGridAndAxis(monitor, id);

		let path = d3.select(`#path-${id}`);
		path.attr('d', lines[id]);
	}

	function stopMonitor() {
		connection.invoke("StopAdvertising").catch(function (err) {
			return console.error(err.toString());
		}).then(() => {
			isConnected = false;
			toggleConnectDisconnect();
			openSaveModal();
		});

		resetApplication();
	}

	function blinkIndexNumbers() {
		for (let i = 0; i < 2; i++) {
			setTimeout(() => {
				d3.selectAll(".indexNumber")
					.transition()
					.duration(250)
					.style("display", "none")
					.on("end", function () {
						d3.select(this).style("display", "block");
					});
			}, 500 * i);
		}
	}

	function openSaveModal() {
		openModal('modalSaveData');

		if (patientId) {
			let patientIdInput = document.getElementById('patientIdConfirm');
			patientIdInput.disabled = true;
			patientIdInput.value = patientId;
		}
	}

	function onSaveClick(save) {
		if (save) {
			let patientIdInput = document.getElementById('patientIdConfirm').value;

			if (!patientId && !patientIdInput) {
				d3.select('#notificationText').style('display', 'block');
				return;
			} else if (!patientId) {
				patientId = patientIdInput;
			}

			saveExcelFile();
		} else {
			deleteExcelFile();
		}
	}

	function saveExcelFile() {
		exportDataConnection.invoke("SaveFile", patientId).catch(function (err) {
			return console.error(err.toString());
		}).then(function () {
			exportDataConnection.stop();
		});

		closeModal('modalSaveData');
	}

	function deleteExcelFile() {
		exportDataConnection.invoke("DeleteTempFile").catch(function (err) {
			return console.error(err.toString());
		}).then(() => {
			closeModal('modalSaveData');
			exportDataConnection.stop();
		});
	}

	function enterPatientId(id) {
		patientId = id;
		let button = document.getElementById('enterPatientIdButton');
		button.innerHTML = `Patient id: ${patientId}`;

		closeModal('modalPatientId');
	}

	function resetApplication() {
		timer.stop();
		graphData = {};
		skinCondGraphData = [];
		clearInterval(interval);
		isFirst = true;
	}

	function createComment() {
		let timestamp = Date.now();
		openModal('modalComment');

		document.getElementById('commentTimestamp').innerHTML = timestamp;
		document.getElementById('commentTimeString').innerHTML = new Date(timestamp).toLocaleTimeString();
	}

	function addComment() {
		let commentTextarea = document.getElementById('commentTextarea');
		let comment = commentTextarea.value;
		let timestamp = document.getElementById('commentTimestamp').innerHTML;
		
		if (comment != "") {
			exportDataConnection.invoke("AddComment", parseInt(timestamp), comment).catch(function (err) {
				return console.error(err.toString());
			}).then(() => {
				commentTextarea.value = "";
			});
		}

		closeModal('modalComment');
	}

	function openModal(id) {
		let modal = d3.select(`#${id}`);
		modal.style('display', 'block');
	}

	function closeModal(id) {
		let modal = d3.select(`#${id}`);
		modal.style('display', 'none');
	}

	function addModalEventHandlers() {
		let modalCloseButton = document.getElementById('closeModal');
		modalCloseButton.onclick = () => { closeModal('modalComment'); };

		let modalComment = document.getElementById('modalComment');
		let modalCloseAppliciation = document.getElementById('modalCloseApplication');
		let modalAddPatientId = document.getElementById('modalPatientId');

		window.onclick = (event) => {
			if (event.target == modalComment) {
				closeModal('modalComment');
			} else if (event.target == modalCloseAppliciation) {
				closeModal('modalCloseApplication');
			} else if (event.target == modalAddPatientId) {
				closeModal('modalPatientId');
			}
		}
	}

	function updateGridAndAxis(monitor, id) {
		monitor.select(`.xAxis#${id}Axis`).call(x[id].axis);
		monitor.select(`.xGrid#${id}Axis`).call(x[id].gridlines);
	}

	function closeApplication() {
		connection.invoke("CloseApplication").catch(function (err) {
			return console.error(err.toString());
		});
	}

	function toggleConnectDisconnect() {
		d3.select('#connectButton').classed('buttonHidden', isConnected);
		d3.select('#disconnectButton').classed('buttonHidden', !isConnected);

		$('#createCommentButton').prop('disabled', !isConnected);
		$('#zoomSC').prop('disabled', !isConnected);
		$('#connectMonitorButton').prop('disabled', !isConnected);
	}

	function toggleConnectToMonitorButton(isConnectedToMonitor) {
		d3.select('#connectMonitorButton').classed('buttonHidden', isConnectedToMonitor);
		d3.select('#disconnectMonitorButton').classed('buttonHidden', !isConnectedToMonitor);
	}

	function connectToMonitor() {
        connection.invoke("ConnectToMonitor").catch(function(err) {
            return console.error(err.toString());
        });
    }

	function disconnectMonitor() {
		connection.invoke("DisconnectMonitor").catch(function (err) {
			return console.error(err.toString());
		}).then(() => {
			toggleConnectToMonitorButton(false);
		});
	}

	$(document).ready(() => {
		connection.start().then(() => {
			checkIfReloaded();
		});
		
		createSkinCondDataMonitor();
		d3.select('.xAxis#skinCondAxis').selectAll('.tick').classed('hide-tick', true);
		d3.select('.axis-y#skinCondAxis').selectAll('.tick').classed('hide-tick', true);
		addModalEventHandlers();
		toggleConnectDisconnect();
		toggleConnectToMonitorButton(false);
	});

	return {
		startMonitor: () => { startMonitor(); },
		stopMonitor: () => { stopMonitor(); },
		createComment: () => { createComment(); },
		addComment: () => { addComment(); },
		closeModal: (id) => { closeModal(id); },
		changeApplication: (application) => { changeApplication(application); },
		zoom: () => { toggleZoom(); },
		skinCondDataGraphChangeZoom: (seconds) => { skinCondDataGraphChangeZoom(seconds); },
		closeApplication: () => { closeApplication(); },
		onSaveClick: (save) => { onSaveClick(save); },
		openModal: (id) => { openModal(id); },
		enterPatientId: (id) => { enterPatientId(id); },
		connectToMonitor: () => { connectToMonitor(); },
		disconnectMonitor: () => { disconnectMonitor(); }
	};

})();