var today;
var sevenDays;
var thirtyDays
var dataTable;

$(document).ready(function () {
    const dateVar = new Date();
    const dateVarTwo = new Date();
    const dateVarThree = new Date();
    dateVarTwo.setDate(dateVarTwo.getDate() + 7);
    dateVarThree.setDate(dateVarTwo.getDate() + 30);
    today = dateVar.toLocaleDateString('en-CA');
    sevenDays = dateVarTwo.toLocaleDateString('en-CA');
    thirtyDays = dateVarThree.toLocaleDateString('en-CA');
    $('#start').val(today);
    $('#end').val(sevenDays);

    // Initialize DataTable
    dataTable = $('#DT_load').DataTable({
        "language": {
            "emptyTable": "No data found."
        },
        "responsive": true,
        "columns": [
            { data: "siteName", width: "20%", title: "Site Number" },
            //{ data: "revenue", width: "20%", title: "Revenue" },
            { data: "reservation", width: "15%", title: "Reservation" },
            {
                data: "checkIn",
                width: "15%",
                title: "Check In",
                render: function (data) {
                    return data ? new Date(data).toLocaleDateString('en-US') : 'N/A';
                }
            },
            {
                data: "checkOut",
                width: "15%",
                title: "Check Out",
                render: function (data) {
                    return data ? new Date(data).toLocaleDateString('en-US') : 'N/A';
                }
            },
            { data: "Status", width: "15%", title: "Status" }
        ],
        "order": [[5, 'asc'], [1, 'asc'], [0, 'asc']],
        "width": "100%",
        "layout": {
            topStart: {
                buttons: ['copy', 'csv', 'excel', 'pdf', 'print']
            }
        }
    });

    // Generate button click
    $('#generateButton').click(function () {
        generateReport();
    });

    // Daily Report button click
    $('#weeklyReportButton').click(function () {
        var todayDate = new Date().toLocaleDateString('en-CA');
        $('#start').val(todayDate);
        $('#end').val(sevenDays);
        generateReport(true);
    });

    // Print Map button click
    $('#printMapButton').click(function () {
        printCampsiteMap();
    });

    // Reset button click
    $('#resetButton').click(function () {
        $('#start').val(today);
        $('#end').val(thirtyDays);
        document.getElementById("table_container").style.display = "none";
        document.getElementById("inputValTwo").style.display = "none";
        document.getElementById("inputValBig").style.display = "none";
    });

    // Make printMapButton always visible
    document.getElementById("printMapButton").style.display = "inline-block";
});

function generateReport(isDailyReport = false) {
    var startDate = $('#start').val();
    var endDate = $('#end').val();
    document.getElementById("inputValBig").style.display = "none";
    document.getElementById("inputValTwo").style.display = "none";

    if (startDate.trim() === '' || endDate.trim() === '') {
        document.getElementById("inputValTwo").style.display = "block";
        return;
    }

    $.ajax({
        url: `/api/financialreport/GetAll?startdate=${startDate}&enddate=${endDate}`,
        type: 'GET',
        success: function (response) {
            console.log('Financial API Response:', JSON.stringify(response, null, 2));
            dataTable.clear().draw();
            dataTable.rows.add(response.data).draw();
            document.getElementById("table_container").style.display = "block";
            dataTable.columns.adjust();
        },
        error: function (xhr, status, error) {
            console.error('Occupancy API Error:', xhr.status, xhr.responseText);
            if (xhr.status == 400) {
                document.getElementById("inputValBig").style.display = "block";
            } else {
                alert("Error fetching report: " + xhr.responseText);
            }
        }
    });
}

function printCampsiteMap() {
    return new Promise((resolve, reject) => {
        const startDate = $('#start').val();
        const endDate = $('#end').val();

        if (startDate.trim() === '' || endDate.trim() === '') {
            document.getElementById("inputValTwo").style.display = "block";
            reject(new Error('Start or end date is empty'));
            return;
        }

        // Show the modal
        const modal = new bootstrap.Modal(document.getElementById('printOptionModal'));
        modal.show();

        // Handle color print
        document.getElementById('printColorButton').onclick = function () {
            modal.hide();
            processPrint(true, startDate, endDate);
        };

        // Handle black-and-white print
        document.getElementById('printBWButton').onclick = function () {
            modal.hide();
            processPrint(false, startDate, endDate);
        };

        function processPrint(isColorPrint, startDate, endDate) {
            // Format date range for display
            const formatDate = (dateStr) => new Date(dateStr).toLocaleDateString('en-US', {
                weekday: 'long',
                year: 'numeric',
                month: 'long',
                day: 'numeric'
            });
            const dateDisplay = startDate === endDate
                ? formatDate(startDate)
                : `${formatDate(startDate)} to ${formatDate(endDate)}`;
            document.getElementById('print-date').textContent = dateDisplay;

            // Update the legend based on print mode
            const legendText = isColorPrint
                ? '<em>Legend: Green = Available • Red = Unavailable</em>'
                : '<em>Legend: White = Available • Black = Unavailable</em>';
            document.querySelector('.customer-message p:last-child').innerHTML = legendText;

            // Update the customer message based on print mode
            const customerMessageText = isColorPrint
                ? 'Please choose any <strong>green (available)</strong> spot on the map and settle in.'
                : 'Please choose any <strong>white (available)</strong> spot on the map and settle in.';
            document.querySelector('.customer-message p:nth-child(2)').innerHTML = customerMessageText;

            updateMapForPrint(isColorPrint, startDate, endDate).then(() => {
                var printSection = document.getElementById('print-section');
                var mapContainer = document.getElementById('campsite-map-container');

                if (!printSection) {
                    console.error('Print section not found in DOM');
                    alert('Failed to generate PDF: Print section not found.');
                    reject(new Error('Print section not found'));
                    return;
                }
                if (isColorPrint) {
                    mapContainer.classList.add('color-print');
                    mapContainer.classList.remove('bw-print');
                } else {
                    mapContainer.classList.add('bw-print');
                    mapContainer.classList.remove('color-print');
                }
                // Ensure the print section is visible and properly sized

                printSection.style.display = 'block';
                printSection.style.width = '900px'; // Increased width for larger SVG
                printSection.style.margin = '0 auto';
                printSection.style.padding = '20px';
                printSection.style.backgroundColor = '#ffffff';
                // Force layout recalculation
                printSection.offsetHeight;

                html2canvas(printSection, {
                    scale: 2,
                    useCORS: true,
                    logging: true,
                    width: 900,
                    height: printSection.offsetHeight,
                    backgroundColor: '#ffffff',
                    allowTaint: true
                }).then(function (canvas) {
                    const { jsPDF } = window.jspdf;
                    const pdf = new jsPDF({
                        orientation: 'landscape',
                        unit: 'px',
                        format: 'letter'
                    });

                    const imgData = canvas.toDataURL('image/png');
                    const imgProps = pdf.getImageProperties(imgData);

                    // Calculate dimensions to fit the entire content
                    const pageWidth = pdf.internal.pageSize.getWidth();
                    const pageHeight = pdf.internal.pageSize.getHeight();

                    const margin = 30;
                    const availableWidth = pageWidth - (2 * margin);
                    const availableHeight = pageHeight - (2 * margin);

                    // Calculate scaling to fit both width and height
                    const widthScale = availableWidth / imgProps.width;
                    const heightScale = availableHeight / imgProps.height;
                    const scale = Math.min(widthScale, heightScale);

                    const pdfWidth = imgProps.width * scale;
                    const pdfHeight = imgProps.height * scale;

                    // Center the content
                    const marginX = (pageWidth - pdfWidth) / 2;
                    const marginY = (pageHeight - pdfHeight) / 2;

                    pdf.addImage(imgData, 'PNG', marginX, marginY, pdfWidth, pdfHeight);
                    pdf.save(`RV_Park_Availability_${startDate}_${endDate}_${isColorPrint ? 'color' : 'bw'}.pdf`);

                    // Reset styles and hide print section
                    printSection.style.display = 'none';
                    printSection.style.width = '';
                    printSection.style.margin = '';
                    printSection.style.padding = '';
                    printSection.style.backgroundColor = '';
                    resolve();
                }).catch(function (error) {
                    console.error('Error generating PDF:', error, error.stack);
                    alert('Failed to generate PDF. Please check the console for details.');

                    // Reset styles and hide print section
                    printSection.style.display = 'none';
                    printSection.style.width = '';
                    printSection.style.margin = '';
                    printSection.style.padding = '';
                    printSection.style.backgroundColor = '';
                    reject(error);
                });
            }).catch(function (error) {
                console.error('Error updating map:', error, error.stack);
                alert('Failed to update map. Please check the console for details.');
                reject(error);
            });
        }
    });
}

function escapeCSSSelector(id) {
    if (!id) {
        console.warn('escapeCSSSelector: ID is undefined or null');
        return null;
    }
    return id.replace(/([0-9])/g, '\\3$1 ').replace(/([#.:])/g, '\\$1');
}

function mapSiteNameToSvgId(siteName) {
    if (!siteName) {
        console.warn('mapSiteNameToSvgId: siteName is undefined or null');
        return null;
    }
    const normalizedSiteName = siteName.trim().toLowerCase().replace(/^site_/i, '');
    const siteMap = {
    };
    const mappedId = siteMap[normalizedSiteName] || normalizedSiteName;
    console.log(`Mapping siteName: ${siteName} (normalized: ${normalizedSiteName}) to SVG ID: ${mappedId}`);
    return mappedId;
}

function updateMapForPrint(isColorPrint, startDate, endDate) {
    return new Promise((resolve, reject) => {
        console.log(`Fetching data for date range: ${startDate} to ${endDate}`);

        // Fetch available sites for the date range
        $.ajax({
            url: `/api/Site/GetAvailableSites?startdate=${startDate}&enddate=${endDate}`,
            type: 'GET',
            success: function (availableSitesResponse) {
                console.log('Available Sites Response:', JSON.stringify(availableSitesResponse, null, 2));

                // Normalize available sites, filter to SVG-relevant sites (1–227, T1–T5)
                const availableSitesData = Array.from(new Set(availableSitesResponse.data.map(site => {
                    if (!site.Name) {
                        console.warn(`Site with SiteId ${site.SiteId} has no Name property`);
                        return null;
                    }
                    return site.Name.toLowerCase().trim().replace(/^site_/i, '');
                }).filter(name => name !== null)))
                    .filter(site => (/^\d+$/.test(site) && parseInt(site) >= 1 && parseInt(site) <= 227) || /^t[1-5]$/i.test(site));

                console.log(`Filtered available sites (1–227, T1–T5): ${availableSitesData.length}`, availableSitesData);

                const problemSites = ['7', '87', '227', '31', '131', '46', 't1', 't2', 't3', 't4', 't5'];
                problemSites.forEach(site => {
                    console.log(`Is site ${site} available? ${availableSitesData.includes(site)}`);
                });

                // Fetch reserved sites for the date range
                $.ajax({
                    url: `/api/occupancyreport/GetAll?startdate=${startDate}&enddate=${endDate}`,
                    type: 'GET',
                    success: function (reservedSitesResponse) {
                        console.log('Reserved Sites Response:', JSON.stringify(reservedSitesResponse, null, 2));

                        // Deduplicate and normalize reserved sites
                        const reservedSitesData = Array.from(new Set(reservedSitesResponse.data.map(site => site.siteName.toLowerCase().trim().replace(/^site_/i, ''))));

                        // Choose SVG based on print mode
                        const svgUrl = isColorPrint ? '/svg/Colorsitemapsvg.svg' : '/svg/BlackandWhiteRVsitemap.svg';
                        console.log('Loading SVG:', svgUrl);

                        fetch(svgUrl)
                            .then(response => {
                                if (!response.ok) {
                                    throw new Error(`Failed to load SVG: ${response.status} ${response.statusText}`);
                                }
                                return response.text();
                            })
                            .then(svgContent => {
                                console.log('SVG Content Loaded, length:', svgContent.length);
                                var parser = new DOMParser();
                                var svgDoc = parser.parseFromString(svgContent, 'image/svg+xml');
                                var svgElement = svgDoc.documentElement;
                                if (!svgElement) {
                                    throw new Error('Failed to parse SVG document');
                                }

                                var originalViewBox = svgElement.getAttribute('viewBox');
                                var originalWidth = svgElement.getAttribute('width');
                                var originalHeight = svgElement.getAttribute('height');

                                console.log('Original SVG dimensions:', {
                                    width: originalWidth,
                                    height: originalHeight,
                                    viewBox: originalViewBox
                                });

                                if (originalViewBox) {
                                    svgElement.setAttribute('width', '800');
                                    svgElement.setAttribute('height', '600');
                                    svgElement.setAttribute('preserveAspectRatio', 'xMidYMid meet');
                                } else {
                                    var width = originalWidth || '800';
                                    var height = originalHeight || '600';
                                    svgElement.setAttribute('viewBox', `0 0 ${width} ${height}`);
                                    svgElement.setAttribute('width', '800');
                                    svgElement.setAttribute('height', '600');
                                    svgElement.setAttribute('preserveAspectRatio', 'xMidYMid meet');
                                }

                                var availableSitesSet = new Set(availableSitesData);
                                var reservedSitesSet = new Set(reservedSitesData);

                                // Log for problem sites
                                problemSites.forEach(site => {
                                    console.log(`Site ${site} in availableSitesSet: ${availableSitesSet.has(site)}`);
                                    console.log(`Site ${site} in reservedSitesSet: ${reservedSitesSet.has(site)}`);
                                });

                                var allSvgElements = svgElement.querySelectorAll('[id]');
                                console.log('All SVG element IDs:', Array.from(allSvgElements).map(el => el.id));

                                // Create a set of valid site IDs (1–227 and T1–T5)
                                const validSiteIds = new Set();
                                allSvgElements.forEach(element => {
                                    const siteId = element.id;
                                    if ((/^\d+$/.test(siteId) && parseInt(siteId) >= 1 && parseInt(siteId) <= 227) || /^t[1-5]$/i.test(siteId)) {
                                        validSiteIds.add(siteId.toLowerCase());
                                    }
                                });
                                console.log('Valid site IDs (1–227, T1–T5):', Array.from(validSiteIds));

                                allSvgElements.forEach(element => {
                                    var siteId = element.id;
                                    if (!siteId) {
                                        console.log('Skipping element with no ID');
                                        return;
                                    }

                                    // Only process rect, polygon, or path elements with valid site IDs (1–227 or T1–T5)
                                    if (!['rect', 'polygon', 'path'].includes(element.tagName.toLowerCase()) ||
                                        !((/^\d+$/.test(siteId) && parseInt(siteId) >= 1 && parseInt(siteId) <= 227) || /^t[1-5]$/i.test(siteId))) {
                                        console.log(`Skipping non-campsite element: ${siteId} (tag: ${element.tagName})`);
                                        return;
                                    }

                                    var normalizedSiteId = siteId.toLowerCase().trim().replace(/^site_/i, '');
                                    var mappedSiteId = mapSiteNameToSvgId(siteId);
                                    var normalizedMappedId = mappedSiteId ? mappedSiteId.toLowerCase().trim().replace(/^site_/i, '') : normalizedSiteId;

                                    var isReserved = reservedSitesSet.has(normalizedSiteId) ||
                                        reservedSitesSet.has(normalizedMappedId) ||
                                        reservedSitesSet.has(siteId.toLowerCase());
                                    // Treat as available if not reserved
                                    var isAvailable = !isReserved;

                                    // Debug for problem sites
                                    if (problemSites.includes(normalizedSiteId) || problemSites.includes(normalizedMappedId)) {
                                        console.log(`Debugging site ${siteId}: siteId=${siteId}, normalizedSiteId=${normalizedSiteId}, mappedSiteId=${mappedSiteId}, isAvailable=${isAvailable}, isReserved=${isReserved}, tag=${element.tagName}`);
                                    }

                                    console.log(`Processing campsite ${siteId} - Available: ${isAvailable}, Reserved: ${isReserved} - Element: ${element.tagName}`);

                                    var originalFill = element.getAttribute('fill') || 'none';
                                    var originalStroke = element.getAttribute('stroke') || 'none';
                                    var originalStrokeWidth = element.getAttribute('stroke-width') || '0';
                                    console.log(`Original attributes for ${siteId}: fill=${originalFill}, stroke=${originalStroke}, stroke-width=${originalStrokeWidth}`);

                                    // Clear any existing styles to prevent default black fills
                                    element.removeAttribute('fill');
                                    element.removeAttribute('stroke');
                                    element.removeAttribute('stroke-width');
                                    element.classList.remove('site-available', 'site-unavailable', 'site-selected');

                                    // If reserved, mark as unavailable
                                    if (isReserved) {
                                        element.classList.add('site-unavailable');
                                        if (isColorPrint) {
                                            element.setAttribute('fill', '#e53e3e'); // Red
                                        } else {
                                            element.setAttribute('fill', '#000000'); // Black
                                        }
                                        element.setAttribute('stroke', '#1a202c');
                                        element.setAttribute('stroke-width', '1');
                                    } else {
                                        // Default to available if not reserved
                                        element.classList.add('site-available');
                                        if (isColorPrint) {
                                            element.setAttribute('fill', '#38a169'); // Green
                                        } else {
                                            element.setAttribute('fill', '#ffffff'); // White
                                        }
                                        element.setAttribute('stroke', '#1a202c');
                                        element.setAttribute('stroke-width', '1');
                                    }
                                });

                                console.log('Available sites:', Array.from(availableSitesSet));
                                console.log('Reserved sites:', Array.from(reservedSitesSet));

                                // Log missing sites instead of warning
                                validSiteIds.forEach(siteId => {
                                    if (!availableSitesSet.has(siteId) && !reservedSitesSet.has(siteId)) {
                                        console.log(`Site ${siteId} not in availableSitesSet but treated as available (not reserved)`);
                                    }
                                });

                                var mapContainer = document.getElementById('campsite-map-container');
                                if (!mapContainer) {
                                    console.error('Campsite map container not found in DOM');
                                    reject(new Error('Campsite map container not found in DOM'));
                                    return;
                                }
                                mapContainer.innerHTML = '';
                                mapContainer.appendChild(svgElement);
                                console.log('SVG appended to map container');
                                resolve();
                            })
                            .catch(error => {
                                console.error('Error loading SVG:', error, error.stack);
                                reject(error);
                            });
                    },
                    error: function (xhr, status, error) {
                        console.error('Error getting reserved sites:', xhr.status, xhr.responseText);
                        reject(error);
                    }
                });
            },
            error: function (xhr, status, error) {
                console.error('Error getting available sites:', xhr.status, xhr.responseText);
                reject(error);
            }
        });
    });
}