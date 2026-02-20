// script-rolepages.js

const API_BASE_URL = '/api'; // << กำหนด API_BASE_URL เป็น Global

// --- Function แสดงข้อความ ---
function showMessage(elementId, text, isSuccess = false) {
    const msgDiv = document.getElementById(elementId);
    if (!msgDiv) return;
    msgDiv.textContent = text;
    msgDiv.className = isSuccess ? 'success' : '';
}

// --- Function ตรวจสอบ Auth และแสดงข้อมูล User ---
function checkAuthAndLoadUserInfo() {
    const userDataString = sessionStorage.getItem('currentUser');
    const userInfoDisplay = document.getElementById('userInfoDisplay');

    if (!userDataString) {
        console.log("Not logged in. Redirecting to login page.");
        window.location.href = 'index.html';
        return null;
    }

    try {
        const userData = JSON.parse(userDataString);
        if (userInfoDisplay) {
            userInfoDisplay.innerHTML = `
                <p>Emp No.: ${userData.employeeNumber || 'N/A'}</p>
                <p>Name: ${userData.firstName || ''} ${userData.lastName || ''}</p>
                <p>Dept: ${userData.department || 'N/A'}</p>
                <p>Position: ${userData.position || 'N/A'}</p>
                 <button id="logoutButton" style="margin-top: 10px;">Logout</button>
            `;
             const logoutBtn = document.getElementById('logoutButton');
             if(logoutBtn) {
                 logoutBtn.addEventListener('click', () => {
                     sessionStorage.removeItem('currentUser');
                     window.location.href = 'index.html';
                 });
             }
        }
        return userData;
    } catch (e) {
        console.error("Error parsing user data:", e);
        sessionStorage.removeItem('currentUser');
        window.location.href = 'index.html';
        return null;
    }
}

// --- ฟังก์ชัน: โหลดข้อมูล Master มาใส่ Dropdown (แก้ไขแล้ว) ---
async function loadMasterDataForDropdowns(deptSelectId, jobSelectId, managerSelectId) {
    const deptSelect = document.getElementById(deptSelectId);
    const jobSelect = document.getElementById(jobSelectId);
    const mgrSelect = document.getElementById(managerSelectId);
    // (API_BASE_URL ควรจะถูกกำหนดไว้ด้านบนสุดของไฟล์นี้แล้ว)

    // 1. โหลด Departments (เหมือนเดิม)
    if (deptSelect) {
        try {
            const response = await fetch(`${API_BASE_URL}/Departments`, { method: 'GET' });
            if (!response.ok) throw new Error('Failed to load departments');
            const departments = await response.json();
            
            deptSelect.innerHTML = '<option value="" disabled selected>-- Select Department --</option>';
            departments.forEach(dept => {
               const option = document.createElement('option');
               option.value = dept.departmentId;
               option.textContent = dept.departmentName;
               deptSelect.appendChild(option);
            });
        } catch (error) { console.error(error); deptSelect.innerHTML = '<option value="">Error loading departments</option>'; }
    }
    
    // 2. โหลด Job Titles (เหมือนเดิม)
    if (jobSelect) {
         try {
            const response = await fetch(`${API_BASE_URL}/JobTitles`, { method: 'GET' });
            if (!response.ok) throw new Error('Failed to load job titles');
            const jobTitles = await response.json();
             
            jobSelect.innerHTML = '<option value="" disabled selected>-- Select Job Title --</option>';
            jobTitles.forEach(job => {
               const option = document.createElement('option');
               option.value = job.jobTitleId;
               option.textContent = job.titleName;
               jobSelect.appendChild(option);
            });
        } catch (error) { console.error(error); jobSelect.innerHTML = '<option value="">Error loading job titles</option>'; }
    }
    
    // 3. ***** แก้ไข: โหลดเฉพาะ Managers *****
    if (mgrSelect) {
         try {
            // ***** เปลี่ยน Endpoint เป็น /Users/managers *****
            const response = await fetch(`${API_BASE_URL}/Users/managers`, { method: 'GET' }); 
            if (!response.ok) throw new Error('Failed to load users for manager list');
            
            const managers = await response.json(); // << ตอนนี้ได้มาเฉพาะ Manager
            
            mgrSelect.innerHTML = '<option value="" selected>-- No Manager --</option>'; // เคลียร์ค่า
            
            managers.forEach(user => { // << วนลูปเฉพาะ Manager
               const option = document.createElement('option');
               option.value = user.employeeId; // <<< ใช้ EmployeeId
               option.textContent = `${user.fullName} (${user.employeeNumber})`;
               mgrSelect.appendChild(option);
            });
        } catch (error) { 
            console.error(error);
            mgrSelect.innerHTML = '<option value="">Error loading managers</option>';
        }
    }
    // ***** จบส่วนแก้ไข *****
}